using UnityEngine;


public class GyroscopeController : MonoBehaviour, IGyroscopeModelSource {
    public enum Integrator { Euler, RK4 }                                       // shows up as a dropdown in the Inspector

    [Header("Gyroscope")]
    [SerializeField] private float mass = 0.1f;                                 // mass in kg
    [SerializeField] private float leverArm = 0.05f;                            // lever arm in m
    [SerializeField] private float radius = 0.03f;                              // disk radius in m

    [Header("Initial conditions")]
    [SerializeField, Range(0f, 3000f)] private float rpm = 3000f;               // omega3 rotations per minute
    [SerializeField, Range(1f, 89f)] private float theta0Deg = 60f;             // initial tilt in degrees
    [SerializeField] private float phiDot0 = 0f;                                // initial precession speed in rad/s

    [Header("Numerics")]
    [SerializeField] private Integrator integrator = Integrator.RK4;            // setting Euler as standard integrator
    [SerializeField, Range(1, 1000)] private int substeps = 10;                 // adjusting the dt

    [SerializeField] private float maxVisibleSpinRps = 5f;                     // revolutions per REAL second
    private float displaySpinDeg;
    public bool SpinDisplayLimited { get; private set; }

    [SerializeField] private TrailRenderer trail;


    [SerializeField] private Transform rotor;
    private GyroscopeModel gyroscope;
    // make the model public
    public GyroscopeModel Model => gyroscope;
    private double e0, simTime, logTimer;
    private readonly double gravity = 9.81;


    // getter, so the UI can read the initial value
    public float Rpm => rpm;
    public float Theta0Deg => theta0Deg;
    public float PhiDot0 => phiDot0; 
    // energy error
    public double EnergyError => (gyroscope.Energy - e0) / (mass * gravity * leverArm);                                                   


    // methods for the UI to restart simulation upon parameter changes
    public void SetRpm(float value) {
        rpm = value;
        ResetSimulation();
    }

    public void SetTheta0Deg(float value) {
        theta0Deg = value;
        ResetSimulation();
    }

    public void SetPhiDot0(float value) {
        phiDot0 = value;
        ResetSimulation();
    }


    public void ResetSimulation() {
        gyroscope = new GyroscopeModel(mass, leverArm , radius, gravity);

        double omega3 = rpm * 2*Mathf.PI / 60f;                                 // figure axis rotaion in rad/s from slider rpm
        double theta0 = Mathf.Deg2Rad * theta0Deg;                              // theta0 in rad

        gyroscope.SetInitialConditions(theta0, 0, phiDot0, omega3);             // setting the initial conditions of the model
        e0 = gyroscope.Energy;                                                  // storing the inital energy

        ApplyPose();                          // safe now: no time step in it
        if (trail != null) trail.Clear();
    }


    void Start() {
        ResetSimulation();
    }


    void OnValidate() {
        if (Application.isPlaying && gyroscope != null) {
            ResetSimulation();
        }
    }


    void FixedUpdate() {
        double h = (double)Time.fixedDeltaTime / substeps;                      // effective dt
        // performing the numerical integration using the appropriate integrator for all substeps
        for (int i = 0; i < substeps; i++) {
            switch (integrator) {
                case Integrator.Euler:
                    gyroscope.EulerStep(h);
                    break;
                case Integrator.RK4:
                    gyroscope.RK4Step(h);
                    break;
                default:
                    break;
            }
        }
        // simulation logging
        simTime += Time.fixedDeltaTime;
        logTimer += Time.fixedDeltaTime;
        if (logTimer >= 0.5) {
            logTimer -= 0.5;
            double deltaE = gyroscope.Energy - e0;
            double deltaENorm = deltaE / (mass * gravity * leverArm);
            Debug.Log($"t = {simTime:F2} s, θ = {gyroscope.Theta * Mathf.Rad2Deg:F3}°, φ̇ = {gyroscope.PhiDot:F3} rad/s, ΔE = {deltaENorm:E2}");
        }
    }


    void Update() {
        UpdateDisplaySpin();
        ApplyPose();
    }


    private void UpdateDisplaySpin() {
        float spinRateDeg = -(float)gyroscope.PsiDot * Mathf.Rad2Deg * Time.timeScale;
        float maxRateDeg = maxVisibleSpinRps * 360f;

        SpinDisplayLimited = Mathf.Abs(spinRateDeg) > maxRateDeg;
        if (SpinDisplayLimited) {
            spinRateDeg = Mathf.Sign(spinRateDeg) * maxRateDeg;                 // maximum spin rate
        }

        displaySpinDeg += spinRateDeg * Time.unscaledDeltaTime;                 // real time
        displaySpinDeg = Mathf.Repeat(displaySpinDeg, 360f);                    // limited to [0, 360]
    }


    private void ApplyPose() {
        // model angles (rad, right-handed) → Unity degrees (left-handed: sign flip)
        float thetaDeg = PhysicsToUnity.Angle(gyroscope.Theta);
        float phiDeg = PhysicsToUnity.Angle(gyroscope.Phi);
        // float psiDeg = PhysicsToUnity.Angle(gyroscope.Psi);

        // rotor
        rotor.localRotation = Quaternion.AngleAxis(displaySpinDeg, Vector3.up);         // around the vertical axis

        // gyroscope
        Quaternion tilt = Quaternion.AngleAxis(thetaDeg, Vector3.right);        // around the x axis
        Quaternion precession = Quaternion.AngleAxis(phiDeg, Vector3.up);       // around the vertical axis
        transform.localRotation = precession * tilt;
    }
}