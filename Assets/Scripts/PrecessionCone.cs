using System;
using UnityEngine;

public class PrecessionCone : MonoBehaviour {
    [SerializeField] private RotorSpin rotor;                           // reference to another component
    [SerializeField, Range(0f, 90f)] private float tiltDeg = 60f;       // θ, angle between axle and vertical

    // phyiscal parameters
    private const float g = 9.81f;                                      // gravitation, m/s²
    private const float r = 0.05f;                                      // centre of mass distance from pivot, m
    private const float R = 0.03f;                                      // disk radius, m
    private float phiDeg;                                               // precession angle φ (state variable)


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start() {
        
    }

    // Update is called once per frame
    void Update() {
        float omega_3 = rotor.Rpm * 2*(float)Math.PI / 60f;             // figure axis rotaion in rad/s from rotors rpm
        float omega_p = 2*g*r / (R*R*omega_3);                          // precession rotation
        phiDeg += omega_p / (2*(float)Math.PI) * 360 * Time.deltaTime;  // euler intagration of the precession angle in degree
        phiDeg = Mathf.Repeat(phiDeg, 360f);                            // restricting the angle to 0 and 360 degrees

    }
}
