using UnityEngine;

public class RotorSpin : MonoBehaviour {
    // slider from 0 to 3000 rpm
    [Tooltip("Rotor spin rate in revolutions per minute")]
    [SerializeField, Range(0f, 3000f)] private float rpm = 10f;

    private float spinAngle;                                            // degrees (Unity uses degrees), about the local Y axis
    public float Rpm => rpm;                                            // read-only property
    private float logTimer;                                             // timer variable for debugging


    // Update is called once per frame
    void Update() {
        float degPerSecond = rpm * 360f / 60;                           // one rpm converts to 360 degrees in 60s  
        spinAngle += degPerSecond * Time.deltaTime;                     // Euler integrator 
        spinAngle = Mathf.Repeat(spinAngle, 360f);                      // forces spinAngle to be between 0 and 360 degrees

        // let the gyroscope (i.e. the rotor) spin
        transform.localRotation = Quaternion.AngleAxis(spinAngle, Vector3.up); 

        // debug log: every second the angle of rotor and the time increment are printed to the console
        logTimer += Time.deltaTime;
        if (logTimer >= 1f) {
            logTimer -= 1f;
            Debug.Log($"spin angle = {spinAngle:F1}°, dt = {Time.deltaTime * 1000f:F2} ms");
        }

    }
}