using System;
using UnityEngine.InputSystem;


public struct TopState {
    public double Theta;                            // rad
    public double ThetaDot;                         // rad/s
    public double Phi;                              // rad
    public double PhiDot;                           // rad/s (unused)
    public double Psi;                              // rad
    public double PsiDot;                           // rad/s (unused)
}




public class GyroscopeModel {
    private readonly double g, l, i1, i3;           // set once in the constructor
    private double pPhi, pPsi;                      // conserved momenta, from the initial conditions
    private TopState state;

    // constructor class
    public GyroscopeModel(double mass, double leverArm, double radius, double gravity = 9.81) {
        double g = gravity;                         // gravity 
        double l = leverArm;                        // distance l between pivot and mass
        double i1 = 1;
        double i3 = 1;
    }

    // initial conditions
    public void SetInitialConditions(double theta0, double thetaDot0, double phiDot0, double omega3) {
        
    }

    // takes TopState struct y (as read-only) and calculates its derivative in TopState struct f
    private void Derivatives(double t, in TopState y, TopState f, double[] parameters) {
        
    }


    // euler integrator: integrates TopState y
    public void EulerStep(double t, double dt, TopState y, in double[] parameters) {
        
    }
}