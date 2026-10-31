using System;
using UnityEngine;
using UnityEngine.Serialization;


// Runs the gyroscope in the module scene: the lab gyroscope or the simple top (one build, the page chooses), with
// parameters in lab language, released or guided start, push, friction.
// The setters only note a change, applied once in the next FixedUpdate, so a whole preset from the page (step 5) costs
// one restart, not one per value. Apparatus and start mode restart; Drehzahl, Zusatzgewicht and Neigung continue the
// running gyroscope from where it is (ContinueSimulation).
public class ModuleGyroscopeController : MonoBehaviour, IGyroscopeModelSource {
    public enum Apparatus { Lab, SimpleTop }                                    // Versuchskreisel / einfacher Kreisel
    public enum StartMode { Released, Guided }                                  // losgelassen / geführt

    [Header("Apparatus")]
    [SerializeField] private Apparatus apparatus = Apparatus.Lab;
    [SerializeField] private ApparatusRig labRig;
    [SerializeField] private ApparatusRig topRig;

    [Header("Experiment")]
    [SerializeField, Range(0f, 3000f)] private float rpm = 400f;                // Drehzahl of the wheel
    [SerializeField, Range(0f, 60f)] private float addOnMassGrams = 30f;        // Zusatzgewicht (lab only)
    [SerializeField, Range(10f, 120f)] private float theta0Deg = 90f;           // Neigung θ₀ of L⃗ (see Theta); clamped to the apparatus' limits
    [SerializeField] private StartMode startMode = StartMode.Guided;
    [FormerlySerializedAs("friction")]                                          // was an on/off bool (0/1) up to v1.1.0
    [SerializeField, Range(0f, 10f)] private float frictionFactor = 0f;         // Lagerreibung: 0 = none, 1 = the apparatus' lab value

    [Header("Push (Stoß)")]
    [SerializeField] private float pushThetaDot = 0.5f;                         // rad/s, knock on the axle from above/below
    [SerializeField] private float pushPhiDot = 0f;                             // rad/s, knock from the side

    [Header("Numerics")]
    [SerializeField, Range(1, 1000)] private int substeps = 10;                 // RK4 steps per FixedUpdate

    [Header("Display")]
    [SerializeField] private float maxVisibleSpinRps = 5f;                      // revolutions per REAL second

    [Header("Readout (written in Play mode)")]
    [SerializeField] private float omegaR;                                      // rad/s
    [SerializeField] private float omegaP;                                      // rad/s
    [SerializeField] private float thetaDeg;
    [SerializeField] private float energyError;

    private GyroscopeModel gyroscope;
    private VectorDisplay vectors;
    private bool resetPending;
    private bool changePending;                                                 // rpm, weight or θ₀: continue, no restart
    private bool tiltPending;                                                   // θ₀ changed: the axle goes to the new tilt
    private bool pushPending;
    private double e0, spinEnergy0;
    private float displaySpinDeg;

    private ApparatusRig Rig => apparatus == Apparatus.Lab ? labRig : topRig;

    public GyroscopeModel Model => gyroscope;
    public bool SpinDisplayLimited { get; private set; }

    public Apparatus SelectedApparatus => apparatus;                            // "Apparatus" is taken by the enum
    public ApparatusRig ActiveRig => Rig;
    public float Rpm => rpm;
    public float AddOnMassGrams => addOnMassGrams;
    public float Theta0Deg => theta0Deg;
    public StartMode SelectedStartMode => startMode;
    public float FrictionFactor => frictionFactor;
    public float PushThetaDot => pushThetaDot;
    public float PushPhiDot => pushPhiDot;

    public double SimTime { get; private set; }                                 // s since the last restart, simulated time
    public int Run { get; private set; }                                        // counts restarts: the page sees when a new run begins
    public int Segment { get; private set; }                                    // counts restarts and parameter changes

    // θ as the page and the Inspector use it: angle between L⃗ (wheel side) and the vertical, as in the module text
    // (dφ = dL/(L sin θ)). The model's figure axis points to the side where the torque acts, on the lab gyroscope the
    // counterweight end, so there θ = π − θ_model. The map is its own inverse: it converts in both directions.
    public double Theta => ThetaOfL(gyroscope.Theta);
    private double ThetaOfL(double theta) => Rig.Parameters.SpinSign < 0 ? Math.PI - theta : theta;

    // readouts as magnitudes (the signs only encode the spin sense)
    public double OmegaR => Math.Abs(gyroscope.PsiDot);                         // what the light barrier sees: wheel against axle
    public double OmegaP => Math.Abs(gyroscope.PhiDot);
    // relative to the initial spin energy (m g l is 0 for the balanced gyroscope); only meaningful without friction
    public double EnergyError => spinEnergy0 > 0 ? (gyroscope.Energy - e0) / spinEnergy0 : 0;


    // ---- called by the UI / bridge ----

    public void SetApparatus(Apparatus value) {
        apparatus = value;
        resetPending = true;
    }

    public void SetRpm(float value) {
        rpm = value;
        changePending = true;
    }

    public void SetAddOnMassGrams(float value) {
        addOnMassGrams = value;
        changePending = true;
    }

    public void SetTheta0Deg(float value) {
        theta0Deg = value;
        changePending = true;
        tiltPending = true;
    }

    public void SetStartMode(StartMode value) {
        startMode = value;
        resetPending = true;
    }

    // no restart: the friction changes while the gyroscope runs (measure T_R before and after).
    // factor × the apparatus' lab value (τ = 120 s); 1 is what the on/off switch of v1.1.0 gave.
    public void SetFriction(float factor) {
        frictionFactor = Mathf.Max(0f, factor);
        if (gyroscope != null) gyroscope.FrictionCoeff = FrictionCoeff(Rig.Parameters);
    }

    private double FrictionCoeff(ApparatusParameters p) => frictionFactor * p.FrictionCoeff;

    [ContextMenu("Neustart")]
    public void Restart() {
        resetPending = true;
    }

    // strength of the next push, no restart
    public void SetPush(float thetaDot, float phiDot) {
        pushThetaDot = thetaDot;
        pushPhiDot = phiDot;
    }

    // applied in the next FixedUpdate, after a pending restart: a preset followed by a push hits the new run
    [ContextMenu("Stoß")]
    public void Push() {
        pushPending = true;
    }


    private void ResetSimulation() {
        ApparatusRig rig = Rig;
        ApparatusParameters p = rig.Parameters;
        labRig.Show(rig == labRig);
        topRig.Show(rig == topRig);
        vectors.SetScales(rig.VectorScales.x, rig.VectorScales.y, rig.VectorScales.z);

        double addOnMass = addOnMassGrams / 1000.0;
        gyroscope = GyroscopeModel.FromInertia(p.I1(addOnMass), p.I3, p.TorqueConst(addOnMass));
        gyroscope.FrictionCoeff = FrictionCoeff(p);

        // spin sense chosen so that L points from the pivot through the wheel
        double omega3 = p.SpinSign * Mathf.Min(rpm, p.MaxRpm) * 2*Math.PI / 60;
        double theta0 = ThetaOfL(Mathf.Clamp(theta0Deg, rig.MinTheta0Deg, rig.MaxTheta0Deg) * Math.PI / 180);   // → model
        double phiDot0 = 0;                                                     // released: the axle starts at rest
        if (startMode == StartMode.Guided) {
            phiDot0 = gyroscope.SteadyPrecessionRate(theta0, omega3);           // guided by hand into steady precession
            if (double.IsNaN(phiDot0)) {
                Debug.LogWarning("Guided start: spin too slow for steady precession, starting released instead.");
                phiDot0 = 0;
            }
        }
        gyroscope.SetInitialConditions(theta0, 0, phiDot0, omega3);

        e0 = gyroscope.Energy;
        spinEnergy0 = 0.5 * p.I3 * omega3*omega3;
        resetPending = changePending = tiltPending = false;
        SimTime = 0;
        Run++;
        Segment++;

        ApplyPose();
        if (rig.Trail != null) rig.Trail.Clear();
    }


    // Drehzahl, Zusatzgewicht or Neigung changed while the gyroscope runs: it goes on from where it is (azimuth, wheel
    // angle, θ̇), with the new values; a new θ₀ tilts the axle at its current azimuth. Guided stays guided: φ̇ shifts by
    // the change of the steady precession rate, so a calm precession stays calm (ω_P steady at once), and any nodding
    // keeps its deviation from it (the balanced gyroscope keeps nutating after a push). SimTime and the trail go on.
    private void ContinueSimulation() {
        ApparatusRig rig = Rig;
        ApparatusParameters p = rig.Parameters;
        GyroscopeModel before = gyroscope;

        double addOnMass = addOnMassGrams / 1000.0;
        gyroscope = GyroscopeModel.FromInertia(p.I1(addOnMass), p.I3, p.TorqueConst(addOnMass));
        gyroscope.FrictionCoeff = FrictionCoeff(p);

        double omega3 = p.SpinSign * Mathf.Min(rpm, p.MaxRpm) * 2*Math.PI / 60;
        double theta = tiltPending
            ? ThetaOfL(Mathf.Clamp(theta0Deg, rig.MinTheta0Deg, rig.MaxTheta0Deg) * Math.PI / 180)
            : before.Theta;
        double phiDot = before.PhiDot;
        if (startMode == StartMode.Guided) {
            double steadyBefore = before.SteadyPrecessionRate(before.Theta, before.Omega3);
            double steadyAfter = gyroscope.SteadyPrecessionRate(theta, omega3);
            if (!double.IsNaN(steadyBefore) && !double.IsNaN(steadyAfter)) phiDot += steadyAfter - steadyBefore;
        }
        gyroscope.SetState(theta, before.Phi, before.Psi, before.ThetaDot, phiDot, omega3);

        e0 = gyroscope.Energy;
        spinEnergy0 = 0.5 * p.I3 * omega3*omega3;
        changePending = tiltPending = false;
        Segment++;
    }


    void Awake() {
        vectors = GetComponent<VectorDisplay>();
    }


    void Start() {
        ResetSimulation();
    }


    // Inspector changes in Play mode: restart once, like a preset
    void OnValidate() {
        if (Application.isPlaying && gyroscope != null) {
            resetPending = true;
        }
    }


    void FixedUpdate() {
        if (resetPending) {
            ResetSimulation();
        } else if (changePending) {
            ContinueSimulation();
        }
        if (pushPending) {
            gyroscope.Push(pushThetaDot, pushPhiDot);
            pushPending = false;
        }
        double h = (double)Time.fixedDeltaTime / substeps;
        for (int i = 0; i < substeps; i++) {
            gyroscope.RK4Step(h);
        }
        SimTime += Time.fixedDeltaTime;
    }


    void Update() {
        // paused (time scale 0): no FixedUpdate, but a new preset, Neustart or a new tilt should show at once
        if (Time.timeScale == 0f) {
            if (resetPending) {
                ResetSimulation();
            } else if (changePending) {
                ContinueSimulation();
            }
        }
        UpdateDisplaySpin();
        ApplyPose();

        omegaR = (float)OmegaR;
        omegaP = (float)OmegaP;
        thetaDeg = (float)(Theta * 180 / Math.PI);
        energyError = (float)EnergyError;
    }


    // same as in GyroscopeController: the drawn spin is limited, the physics is not
    private void UpdateDisplaySpin() {
        float spinRateDeg = -(float)gyroscope.PsiDot * Mathf.Rad2Deg * Time.timeScale;
        float maxRateDeg = maxVisibleSpinRps * 360f;

        SpinDisplayLimited = Mathf.Abs(spinRateDeg) > maxRateDeg;
        if (SpinDisplayLimited) {
            spinRateDeg = Mathf.Sign(spinRateDeg) * maxRateDeg;
        }

        displaySpinDeg += spinRateDeg * Time.unscaledDeltaTime;                 // real time
        displaySpinDeg = Mathf.Repeat(displaySpinDeg, 360f);
    }


    // model angles (rad, right-handed) → Unity degrees (left-handed: sign flip); local up of this object = figure axis
    private void ApplyPose() {
        float tiltDeg = PhysicsToUnity.Angle(gyroscope.Theta);
        float phiDeg = PhysicsToUnity.Angle(gyroscope.Phi);

        Rig.Rotor.localRotation = Quaternion.AngleAxis(displaySpinDeg, Vector3.up);

        Quaternion tilt = Quaternion.AngleAxis(tiltDeg, Vector3.right);
        Quaternion precession = Quaternion.AngleAxis(phiDeg, Vector3.up);
        transform.localRotation = precession * tilt;
    }
}
