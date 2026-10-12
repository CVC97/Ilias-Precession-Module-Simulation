using System;


public struct TopState {
    public double Theta;                                    // rad
    public double Phi;                                      // rad
    public double Psi;                                      // rad
    public double ThetaDot;                                 // rad/s
}



public class GyroscopeModel {
    private readonly double m, g, l, i1, i3;                // set once in the constructor
    private double pPhi, pPsi;                              // conserved momenta, from the initial conditions
    private TopState state;


    // read-only properties
    public double Theta  => state.Theta;
    public double Phi  => state.Phi;
    public double Psi  => state.Psi;
    // public double Energy => 1;


    // constructor class
    public GyroscopeModel(double mass, double leverArm, double radius, double gravity = 9.81) {
        m = mass;                                           // mass
        g = gravity;                                        // gravity 
        l = leverArm;                                       // distance l between pivot and mass
        i1 = 1.0/4*mass*Math.Pow(radius, 2) + mass*NPow(l, 2);
        i3 = 1.0/2*mass*Math.Pow(radius, 2);
    }


    private double NPow(double x, int n) {
        double prod = x;
        for (int i = 1; i < n; i++) {
            prod *= x;
        }
        return prod;
    }


    // helper functions to calculate PhiDot, PsiDot, ThetaDotDot
    private double CalcPhiDot(in TopState y) {
        double theta = y.Theta;
        double sinTheta = Math.Sin(theta);
        double cosTheta = Math.Cos(theta);

        double phiDot = (pPhi - pPsi*cosTheta) / (i1*NPow(sinTheta, 2));
        return phiDot;
    }

    private double CalcPsiDot(in TopState y) {
        double theta = y.Theta;
        double cosTheta = Math.Cos(theta);

        double phiDot = CalcPhiDot(y);
        double psiDot = pPsi/i3 - phiDot*cosTheta;
        return psiDot;
    }

    private double CalcThetaDotDot(in TopState y) {
        double theta = y.Theta;
        double sinTheta = Math.Sin(theta);
        double cosTheta = Math.Cos(theta);

        double phiDot = CalcPhiDot(y);
        double psiDot = CalcPsiDot(y);
        double thetaDotDot = (1-i3/i1)*NPow(phiDot, 2)*sinTheta*cosTheta - i3/i1*phiDot*psiDot*sinTheta + m*g*l*sinTheta/i1;
        return thetaDotDot;
    }


    // initial conditions
    public void SetInitialConditions(double theta0, double thetaDot0, double phiDot0, double omega3) {
        // calculating the conserved momenta pPhi and pPsi
        pPhi = i1 * NPow(Math.Sin(theta0), 2) * phiDot0 + i3 * Math.Cos(theta0) * omega3;
        pPsi = i3 * omega3;
        // creating the initial state of the system
        state.Theta = theta0;
        state.Phi = 0;
        state.Psi = 0;
        state.ThetaDot = thetaDot0;
    }


    // takes TopState struct y (as read-only) and calculates its derivative in TopState struct f (output-only)
    private void Derivatives(in TopState y, out TopState f) {
        // calculating the derivative of TopState y
        double thetaDot = y.ThetaDot;
        double phiDot = CalcPhiDot(y);
        double psiDot = CalcPsiDot(y);
        double thetaDotDot = CalcThetaDotDot(y);

        // using the state variables to calculate the derivatives array f
        f.Theta = thetaDot;                                 // thetaDot
        f.Phi = phiDot;                                     // phiDot
        f.Psi = psiDot;                                     // psiDot
        f.ThetaDot = thetaDotDot;                           // thetaDotDot
    }


    // euler integrator: integrates TopState y
    public void EulerStep(double dt, TopState state) {
        Derivatives(state, out TopState f);

        // updating the state component by component
        state.Theta += f.Theta * dt;
        state.Phi += f.Phi * dt;
        state.Psi += f.Psi * dt;
        state.ThetaDot += f.ThetaDot * dt;
    }


    // // verlet integrator: integrates TopState y
    // public void VerletStep(double dt, TopState y) {
    //     Derivatives(state, out TopState a1);

    //     // updating the angles with a1
    //     state.Theta += a1.Theta * dt + a1.Theta * NPow(dt, 2) / 2;
    //     state.ThetaDot += a1.ThetaDot * dt + a1.ThetaDot * NPow(dt, 2) / 2;

    //     Derivatives(state, out TopState a2);
        
    // }


    // rk4 integrator: integrates TopState y
    public void RK4Step(double dt, TopState state) {
        TopState support;
        // calculation k1 = f(t, y) * dt and support = y + k1/2
        Derivatives(state, out TopState k1);
        k1.Theta *= dt;
        k1.Phi *= dt;
        k1.Psi *= dt;
        k1.ThetaDot *= dt;
        support.Theta = state.Theta + k1.Theta/2;
        support.Phi = state.Phi + k1.Phi/2;
        support.Psi = state.Psi + k1.Psi/2;
        support.ThetaDot = state.ThetaDot + k1.ThetaDot/2;
        // calculation k2 = f(t+dt/2, y+k1/2) * dt and support = y + k2/2
        Derivatives(support, out TopState k2);
        k2.Theta *= dt;
        k2.Phi *= dt;
        k2.Psi *= dt;
        k2.ThetaDot *= dt;
        support.Theta = state.Theta + k2.Theta/2;
        support.Phi = state.Phi + k2.Phi/2;
        support.Psi = state.Psi + k2.Psi/2;
        support.ThetaDot = state.ThetaDot + k2.ThetaDot/2;
        // calculation k3 = f(t+dt/2, y+k2/2) * dt and support = y + k3
        Derivatives(support, out TopState k3);
        k3.Theta *= dt;
        k3.Phi *= dt;
        k3.Psi *= dt;
        k3.ThetaDot *= dt;
        support.Theta = state.Theta + k3.Theta;
        support.Phi = state.Phi + k3.Phi;
        support.Psi = state.Psi + k3.Psi;
        support.ThetaDot = state.ThetaDot + k3.ThetaDot;
        // calculation k4 = f(t+dt, y+k2) * dt and y_(i+1)
        Derivatives(support, out TopState k4);
        k3.Theta *= dt;
        k3.Phi *= dt;
        k3.Psi *= dt;
        k3.ThetaDot *= dt;
        state.Theta += (k1.Theta + 2*k2.Theta + 2*k3.Theta + k4.Theta) / 6;
        state.Phi += (k1.Phi + 2*k2.Phi + 2*k3.Phi + k4.Phi) / 6;
        state.Psi += (k1.Psi + 2*k2.Psi + 2*k3.Psi + k4.Psi) / 6;
        state.ThetaDot += (k1.ThetaDot + 2*k2.ThetaDot + 2*k3.ThetaDot + k4.ThetaDot) / 6; 
    }
}