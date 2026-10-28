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
}
