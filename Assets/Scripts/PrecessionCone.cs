using UnityEngine;

public class PrecessionCone : MonoBehaviour {
    [SerializeField] private RotorSpin rotor;                                   // reference to another component
    [SerializeField, Range(0f, 90f)] private float tiltDeg = 60f;               // θ, angle between axle and vertical

    // phyiscal parameters
    private const float G = 9.81f;                                              // gravitation, m/s²
    private const float RCm = 0.05f;                                            // centre of mass distance from pivot, m
    private const float RRotor = 0.03f;                                         // disk radius, m
    private float phiDeg;                                                       // precession angle φ (state variable)

    // helper variables
    private float logTp;                                                        // timer variable for debugging
    private const float EpsMax = 0.05f;                                         // max Ω/ω₃ for the gyroscopic approximation
    private static readonly float Omega3Min = Mathf.Sqrt(2f * G * RCm / (EpsMax * RRotor * RRotor));
    private bool warnedLowRpm;


    // Update is called once per frame
    void Update() {
        float omega3 = rotor.Rpm * 2*Mathf.PI / 60f;                            // figure axis rotaion in rad/s from rotors rpm
        // low rpm guard
        if (Mathf.Abs(omega3) > Omega3Min) {
            float omegaP = 2*G*RCm / (RRotor*RRotor*omega3);                    // precession rotation
            phiDeg += omegaP * Mathf.Rad2Deg * Time.deltaTime;                  // euler intagration of the precession angle in degree

            // debug log: each full precession rotation, i.e. T_precession
            logTp += Time.deltaTime;
            if (phiDeg >= 360f || phiDeg < 0f) {
                Debug.Log($"precession angle = {Mathf.Repeat(phiDeg, 360f):F1}°, T_precession = {logTp:F2} s");
                logTp = 0;
            }
            phiDeg = Mathf.Repeat(phiDeg, 360f);                                // restricting the angle to 0 and 360 degrees

            warnedLowRpm = false;
        } else {
            if (!warnedLowRpm) {
                Debug.LogWarning("RPM too low, approximation breaks down.");
                warnedLowRpm = true;
            } 

        }

        // applying tilt and precession to the entire gyroscope (tilt first, precession afterwards)
        Quaternion tilt = Quaternion.AngleAxis(tiltDeg, Vector3.right);         // around the x axis
        Quaternion precession = Quaternion.AngleAxis(phiDeg, Vector3.up);       // around the vertical axis
        transform.localRotation = precession * tilt;
        // Quaternion.Euler(tiltDeg, phiDeg, 0)                                 // equivalent to above, just shorter
    }
}
