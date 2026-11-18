using System;
using NUnit.Framework;


// Checks of the extended GyroscopeModel with lab-gyroscope parameters. Run in Unity: Window → General → Test Runner →
// EditMode → Run All. Plain NUnit, no UnityEngine, so the same file also runs outside Unity.
public class GyroscopeModelTests {
    // rounded lab gyroscope (see learning_migration.md, A3): moments of inertia about pivot and figure axis
    private const double I1 = 0.052;                        // kg·m², incl. 30 g add-on weight
    private const double I3 = 0.01;                         // kg·m²
    private const double Loaded = 0.03 * 9.81 * 0.27;       // N·m, torque constant m_z g r for 30 g at the notch
    private const double Omega3 = 40;                       // rad/s, ≈ 380 rpm
    private const double Horizontal = Math.PI / 2;


    private static double Deg(double deg) => deg * Math.PI / 180;


    private static void Run(GyroscopeModel model, double seconds, double dt, Action<double> afterStep = null) {
        int steps = (int)Math.Round(seconds / dt);
        for (int i = 1; i <= steps; i++) {
            model.RK4Step(dt);
            afterStep?.Invoke(i * dt);
        }
    }


    [TestCase(0.0, TestName = "Energy conserved, balanced")]
    [TestCase(Loaded, TestName = "Energy conserved, loaded")]
    public void EnergyIsConserved(double torqueConst) {
        var model = GyroscopeModel.FromInertia(I1, I3, torqueConst);
        model.SetInitialConditions(Horizontal, 0, 0, Omega3);
        model.Push(0.5, 0);                                 // nutation on top of everything
        double e0 = model.Energy;

        Run(model, 10, 1e-3);

        Assert.That(Math.Abs(model.Energy - e0) / e0, Is.LessThan(1e-9));
    }


    [Test]
    public void SteadyPrecessionAtHorizontalEqualsFastTopFormula() {
        var model = GyroscopeModel.FromInertia(I1, I3, Loaded);
        double expected = Loaded / (I3 * Omega3);           // ω_P = m_z g r / (I3 ω_R)

        Assert.That(model.SteadyPrecessionRate(Horizontal, Omega3), Is.EqualTo(expected).Within(1e-12 * expected));
    }


    [TestCase(60.0)]
    [TestCase(90.0)]
    [TestCase(120.0)]
    public void GuidedStartKeepsThetaConstant(double theta0Deg) {
        var model = GyroscopeModel.FromInertia(I1, I3, Loaded);
        double theta0 = Deg(theta0Deg);
        model.SetInitialConditions(theta0, 0, model.SteadyPrecessionRate(theta0, Omega3), Omega3);
        double maxDeviation = 0;

        Run(model, 5, 1e-3, t => maxDeviation = Math.Max(maxDeviation, Math.Abs(model.Theta - theta0)));

        Assert.That(maxDeviation, Is.LessThan(1e-9));
    }


    // reversed spin (the module lets L point through the wheel): same magnitude, opposite sense, θ stays put
    [Test]
    public void GuidedStartWithReversedSpin() {
        var model = GyroscopeModel.FromInertia(I1, I3, Loaded);
        double rate = model.SteadyPrecessionRate(Deg(70), -Omega3);
        model.SetInitialConditions(Deg(70), 0, rate, -Omega3);
        double maxDeviation = 0;

        Run(model, 5, 1e-3, t => maxDeviation = Math.Max(maxDeviation, Math.Abs(model.Theta - Deg(70))));

        Assert.That(rate, Is.LessThan(0));
        Assert.That(-rate, Is.EqualTo(model.SteadyPrecessionRate(Deg(70), Omega3)).Within(1e-12));    // φ̇ → −φ̇ for ω3 → −ω3
        Assert.That(maxDeviation, Is.LessThan(1e-9));
    }


    [TestCase(60.0)]
    [TestCase(120.0)]
    public void PrecessionRateNearlyIndependentOfTheta(double thetaDeg) {
        var model = GyroscopeModel.FromInertia(I1, I3, Loaded);
        double atHorizontal = model.SteadyPrecessionRate(Horizontal, Omega3);

        // fast top: D = k sinθ and the horizontal part of L, I3 ω3 sinθ, both scale with sinθ
        Assert.That(model.SteadyPrecessionRate(Deg(thetaDeg), Omega3), Is.EqualTo(atHorizontal).Within(3).Percent);
    }


    [Test]
    public void NoSteadyPrecessionWithoutSpin() {
        var model = GyroscopeModel.FromInertia(I1, I3, Loaded);

        Assert.That(model.SteadyPrecessionRate(Horizontal, 0), Is.NaN);
    }


    // force-free (balanced) top after a push: the figure axis circles L with ω = L/I1, so θ oscillates with T = 2π I1/L
    [TestCase(20.0)]
    [TestCase(40.0)]
    [TestCase(80.0)]
    public void NutationPeriodOfBalancedTop(double omega3) {
        var model = GyroscopeModel.FromInertia(I1, I3, 0);
        model.SetInitialConditions(Horizontal, 0, 0, omega3);
        model.Push(0.5, 0);
        Vec3d l = model.AngularMomentum;
        double expected = 2 * Math.PI * I1 / Math.Sqrt(Vec3d.Dot(l, l));

        // times of the maxima of θ: θ̇ changes sign from + to − (linear interpolation between the steps)
        double dt = 1e-4, previousThetaDot = model.ThetaDot, firstMax = double.NaN, lastMax = double.NaN;
        int maxima = 0;
        Run(model, 8 * expected, dt, t => {
            if (previousThetaDot > 0 && model.ThetaDot <= 0) {
                double tMax = t - dt * model.ThetaDot / (model.ThetaDot - previousThetaDot);
                if (maxima == 0) firstMax = tMax;
                lastMax = tMax;
                maxima++;
            }
            previousThetaDot = model.ThetaDot;
        });

        double measured = (lastMax - firstMax) / (maxima - 1);
        Assert.That(maxima, Is.GreaterThan(3));
        Assert.That(measured, Is.EqualTo(expected).Within(1e-6 * expected));
    }


    [Test]
    public void FrictionSlowsTheSpinExponentially() {
        const double tau = 10;                              // s, decay time of the spin
        var model = GyroscopeModel.FromInertia(I1, I3, 0);
        model.SetInitialConditions(Horizontal, 0, 0, Omega3);
        model.FrictionCoeff = I3 / tau;                     // horizontal axle, no precession: ψ̇ = ω3 → ω̇3 = −ω3/τ

        Run(model, tau, 1e-3);

        Assert.That(model.Omega3, Is.EqualTo(Omega3 * Math.Exp(-1)).Within(1e-9 * Omega3));
    }


    // bearing friction is internal (wheel against axle): the vertical angular momentum L_z = p_φ stays
    [Test]
    public void FrictionKeepsVerticalAngularMomentum() {
        var model = GyroscopeModel.FromInertia(I1, I3, Loaded);
        model.SetInitialConditions(Deg(60), 0, 0, Omega3);
        model.FrictionCoeff = I3 / 10;
        double lz0 = model.AngularMomentum.Z;

        Run(model, 5, 1e-3);

        Assert.That(model.Omega3, Is.LessThan(0.7 * Omega3));
        Assert.That(model.AngularMomentum.Z, Is.EqualTo(lz0).Within(1e-12));
    }


    [Test]
    public void PushChangesVelocitiesButNotSpin() {
        var model = GyroscopeModel.FromInertia(I1, I3, Loaded);
        model.SetInitialConditions(Deg(80), 0.1, 0.2, Omega3);
        double thetaDot = model.ThetaDot, phiDot = model.PhiDot;

        model.Push(0.5, -0.3);

        Assert.That(model.ThetaDot, Is.EqualTo(thetaDot + 0.5).Within(1e-12));
        Assert.That(model.PhiDot, Is.EqualTo(phiDot - 0.3).Within(1e-12));
        Assert.That(model.Omega3, Is.EqualTo(Omega3).Within(1e-12));
    }


    // a slider change in the module continues the run (ModuleGyroscopeController.ContinueSimulation): the axle keeps
    // its azimuth and, guided at the new steady rate, precesses on calmly at twice the spin, half the rate
    [Test]
    public void ContinuedStateKeepsAzimuthAndGuidedPrecession() {
        var model = GyroscopeModel.FromInertia(I1, I3, Loaded);
        model.SetInitialConditions(Horizontal, 0, model.SteadyPrecessionRate(Horizontal, Omega3), Omega3);
        Run(model, 3, 1e-3);
        double phi = model.Phi, psi = model.Psi;

        var continued = GyroscopeModel.FromInertia(I1, I3, Loaded);
        continued.SetState(Horizontal, phi, psi, 0, continued.SteadyPrecessionRate(Horizontal, 2 * Omega3), 2 * Omega3);
        double maxDeviation = 0;
        Assert.That(continued.Phi, Is.EqualTo(phi));
        Assert.That(continued.Psi, Is.EqualTo(psi));

        Run(continued, 5, 1e-3, t => maxDeviation = Math.Max(maxDeviation, Math.Abs(continued.Theta - Horizontal)));

        Assert.That(maxDeviation, Is.LessThan(1e-9));
        Assert.That(continued.PhiDot, Is.EqualTo(model.PhiDot / 2).Within(1e-3 * model.PhiDot));
    }


    // ---- lecture gyroscope (LectureGyroscope.asset, estimates): figure axis along the rod to the disk, nearly upright.
    // Bare it hangs (k < 0, heavy plate below the tip), with 300 g on the hub it is top-heavy (k > 0).
    private const double LectureI3 = 0.0406;                // kg·m²
    private const double LectureI1Bare = 0.208;             // kg·m²
    private const double LectureI1Heavy = 0.377;            // kg·m², with 300 g on the hub
    private const double Hanging = -0.883;                  // N·m
    private const double TopHeavy = 1.324;                  // N·m
    private const double LectureOmega3 = 300 * 2 * Math.PI / 60;    // rad/s, 300 rpm


    [TestCase(Hanging, LectureI1Bare, TestName = "Energy conserved, hanging lecture top")]
    [TestCase(TopHeavy, LectureI1Heavy, TestName = "Energy conserved, top-heavy lecture top")]
    public void LectureTopEnergyIsConserved(double torqueConst, double i1) {
        var model = GyroscopeModel.FromInertia(i1, LectureI3, torqueConst);
        model.SetInitialConditions(Deg(15), 0, 0, LectureOmega3);
        model.Push(0.3, 0);
        double e0 = model.Energy;
        double scale = 0.5 * LectureI3 * LectureOmega3 * LectureOmega3;

        Run(model, 10, 1e-3);

        Assert.That(Math.Abs(model.Energy - e0) / scale, Is.LessThan(1e-9));
    }


    // the hanging top precesses the other way round: same spin, opposite torque, opposite φ̇; for a fast top
    // φ̇ → k/(I3 ω3) in both cases
    [Test]
    public void HangingTopPrecessesBackwards() {
        const double fastOmega3 = 300;                      // rad/s, fast enough for the fast-top formula
        var hanging = GyroscopeModel.FromInertia(LectureI1Bare, LectureI3, Hanging);
        var topHeavy = GyroscopeModel.FromInertia(LectureI1Bare, LectureI3, -Hanging);

        double backwards = hanging.SteadyPrecessionRate(Deg(15), fastOmega3);
        double forwards = topHeavy.SteadyPrecessionRate(Deg(15), fastOmega3);

        Assert.That(backwards, Is.LessThan(0));
        Assert.That(forwards, Is.GreaterThan(0));
        Assert.That(backwards, Is.EqualTo(Hanging / (LectureI3 * fastOmega3)).Within(1).Percent);
        Assert.That(forwards, Is.EqualTo(-Hanging / (LectureI3 * fastOmega3)).Within(1).Percent);
    }


    [TestCase(5.0)]
    [TestCase(25.0)]
    public void GuidedHangingTopKeepsThetaConstant(double theta0Deg) {
        var model = GyroscopeModel.FromInertia(LectureI1Bare, LectureI3, Hanging);
        double theta0 = Deg(theta0Deg);
        model.SetInitialConditions(theta0, 0, model.SteadyPrecessionRate(theta0, LectureOmega3), LectureOmega3);
        double maxDeviation = 0;

        Run(model, 10, 1e-3, t => maxDeviation = Math.Max(maxDeviation, Math.Abs(model.Theta - theta0)));

        Assert.That(maxDeviation, Is.LessThan(1e-9));
    }


    // a hanging top has a steady precession at any spin (the discriminant a² − 4 b cosθ stays positive for b < 0),
    // a top-heavy one only above I3² ω3² = 4 k I1 cosθ: at 15° about 330 rpm for 300 g
    [Test]
    public void TopHeavyLectureTopNeedsFastSpin() {
        var hanging = GyroscopeModel.FromInertia(LectureI1Bare, LectureI3, Hanging);
        var topHeavy = GyroscopeModel.FromInertia(LectureI1Heavy, LectureI3, TopHeavy);
        double limit = Math.Sqrt(4 * TopHeavy * LectureI1Heavy * Math.Cos(Deg(15))) / LectureI3;   // rad/s

        Assert.That(hanging.SteadyPrecessionRate(Deg(15), 1), Is.Not.NaN);
        Assert.That(topHeavy.SteadyPrecessionRate(Deg(15), LectureOmega3), Is.NaN);         // 300 rpm: too slow
        Assert.That(topHeavy.SteadyPrecessionRate(Deg(15), 0.99 * limit), Is.NaN);
        Assert.That(topHeavy.SteadyPrecessionRate(Deg(15), 1.01 * limit), Is.Not.NaN);
        Assert.That(limit * 60 / (2 * Math.PI), Is.EqualTo(327).Within(2).Percent);
    }


    // released at 300 rpm the top-heavy top swings far down (it would hit the stop at ~30°), the hanging one only
    // nods and comes back up
    [Test]
    public void ReleasedSlowTopHeavyTopFallsFarHangingDoesNot() {
        var topHeavy = GyroscopeModel.FromInertia(LectureI1Heavy, LectureI3, TopHeavy);
        var hanging = GyroscopeModel.FromInertia(LectureI1Bare, LectureI3, Hanging);
        topHeavy.SetInitialConditions(Deg(15), 0, 0, LectureOmega3);
        hanging.SetInitialConditions(Deg(15), 0, 0, LectureOmega3);
        double maxHeavy = 0, maxHanging = 0;

        Run(topHeavy, 5, 1e-3, t => maxHeavy = Math.Max(maxHeavy, topHeavy.Theta));
        Run(hanging, 5, 1e-3, t => maxHanging = Math.Max(maxHanging, hanging.Theta));

        Assert.That(maxHeavy, Is.GreaterThan(Deg(30)));
        Assert.That(maxHanging, Is.LessThanOrEqualTo(Deg(15) + 1e-9));
    }
}
