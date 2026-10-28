using System;
using UnityEngine;


// Runs the lab gyroscope in the module scene: parameters in lab language, released or guided start, push, friction.
// The setters only note a change; the simulation restarts once in the next FixedUpdate. A whole preset from the page
// (step 5) therefore costs one restart, not one per value.
public class ModuleGyroscopeController : MonoBehaviour, IGyroscopeModelSource {
    public enum StartMode { Released, Guided }                                  // losgelassen / geführt

    [SerializeField] private LabGyroscope lab;

    [Header("Experiment")]
    [SerializeField, Range(0f, 1000f)] private float rpm = 400f;                // Drehzahl of the wheel
    [SerializeField, Range(0f, 60f)] private float addOnMassGrams = 30f;        // Zusatzgewicht
    [SerializeField, Range(60f, 120f)] private float theta0Deg = 90f;           // Neigung θ₀; 90° = horizontal axle
                                                                                // (beyond ±30° the wheel would hit the stand)
    [SerializeField] private StartMode startMode = StartMode.Guided;
    [SerializeField] private bool friction = false;                             // Reibung
    // spin sense: the model's figure axis points to the counterweight side, so a negative ω3 makes L point from the
    // pivot through the wheel, as in textbook figures; precession then runs the other way round (same magnitude)
    [SerializeField] private bool angularMomentumThroughWheel = true;

    [Header("Push (Stoß)")]
    [SerializeField] private float pushThetaDot = 0.5f;                         // rad/s, knock on the axle from above/below
    [SerializeField] private float pushPhiDot = 0f;                             // rad/s, knock from the side

    [Header("Numerics")]
    [SerializeField, Range(1, 1000)] private int substeps = 10;                 // RK4 steps per FixedUpdate

    [Header("Display")]
    [SerializeField] private float maxVisibleSpinRps = 5f;                      // revolutions per REAL second
    [SerializeField] private Transform rotor;
    [SerializeField] private TrailRenderer trail;

    [Header("Readout (written in Play mode)")]
    [SerializeField] private float omegaR;                                      // rad/s
    [SerializeField] private float omegaP;                                      // rad/s
    [SerializeField] private float thetaDeg;
    [SerializeField] private float energyError;

    private GyroscopeModel gyroscope;
    private bool resetPending;
    private double e0, spinEnergy0;
    private float displaySpinDeg;

    public GyroscopeModel Model => gyroscope;
    public bool SpinDisplayLimited { get; private set; }

    public float Rpm => rpm;
    public float AddOnMassGrams => addOnMassGrams;
    public float Theta0Deg => theta0Deg;
    public StartMode SelectedStartMode => startMode;                            // "StartMode" is taken by the enum
    public bool Friction => friction;

    // readouts as magnitudes (the signs only encode the spin sense)
    public double OmegaR => Math.Abs(gyroscope.PsiDot);                         // what the light barrier sees: wheel against axle
    public double OmegaP => Math.Abs(gyroscope.PhiDot);
    // relative to the initial spin energy (m g l is 0 for the balanced gyroscope); only meaningful without friction
    public double EnergyError => spinEnergy0 > 0 ? (gyroscope.Energy - e0) / spinEnergy0 : 0;


    // ---- called by the UI / bridge ----

    public void SetRpm(float value) {
        rpm = value;
        resetPending = true;
    }

    public void SetAddOnMassGrams(float value) {
        addOnMassGrams = value;
        resetPending = true;
    }

    public void SetTheta0Deg(float value) {
        theta0Deg = value;
        resetPending = true;
    }

    public void SetStartMode(StartMode value) {
        startMode = value;
        resetPending = true;
    }

    // no restart: friction can be switched on while the gyroscope runs (measure T_R before and after)
    public void SetFriction(bool on) {
        friction = on;
        if (gyroscope != null) gyroscope.FrictionCoeff = on ? lab.FrictionCoeff : 0;
    }

    [ContextMenu("Neustart")]
    public void Restart() {
        resetPending = true;
    }

    [ContextMenu("Stoß")]
    public void Push() {
        gyroscope.Push(pushThetaDot, pushPhiDot);
    }


    private void ResetSimulation() {
        double addOnMass = addOnMassGrams / 1000.0;
        gyroscope = GyroscopeModel.FromInertia(lab.I1(addOnMass), lab.I3, lab.TorqueConst(addOnMass));
        gyroscope.FrictionCoeff = friction ? lab.FrictionCoeff : 0;

        double omega3 = (angularMomentumThroughWheel ? -1 : 1) * rpm * 2*Math.PI / 60;
        double theta0 = theta0Deg * Math.PI / 180;
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
        spinEnergy0 = 0.5 * lab.I3 * omega3*omega3;
        resetPending = false;

        ApplyPose();
        if (trail != null) trail.Clear();
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
        }
        double h = (double)Time.fixedDeltaTime / substeps;
        for (int i = 0; i < substeps; i++) {
            gyroscope.RK4Step(h);
        }
    }


    void Update() {
        UpdateDisplaySpin();
        ApplyPose();

        omegaR = (float)OmegaR;
        omegaP = (float)OmegaP;
        thetaDeg = (float)(gyroscope.Theta * 180 / Math.PI);
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

        rotor.localRotation = Quaternion.AngleAxis(displaySpinDeg, Vector3.up);

        Quaternion tilt = Quaternion.AngleAxis(tiltDeg, Vector3.right);
        Quaternion precession = Quaternion.AngleAxis(phiDeg, Vector3.up);
        transform.localRotation = precession * tilt;
    }
}
