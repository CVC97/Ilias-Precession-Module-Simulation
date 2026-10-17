using System;


public struct TopState {
    public double Theta;                                    // rad
    public double Phi;                                      // rad
    public double Psi;                                      // rad
    public double ThetaDot;                                 // rad/s

    // operator overloading
    public static TopState operator +(TopState a, TopState b) =>
        new TopState {
            Theta = a.Theta + b.Theta,
            Phi = a.Phi + b.Phi,
            Psi = a.Psi + b.Psi,
            ThetaDot = a.ThetaDot + b.ThetaDot,
        };

    public static TopState operator *(TopState a, double t) =>
        new TopState {
            Theta = a.Theta * t,
            Phi = a.Phi * t,
            Psi = a.Psi * t,
            ThetaDot = a.ThetaDot * t,
        };
}



public class GyroscopeModel {
    private readonly double m, g, l, i1, i3;                // set once in the constructor
    private double pPhi, pPsi;                              // conserved momenta, from the initial conditions
    private TopState state;

    // read-only properties
    public double Theta => state.Theta;
    public double Phi => state.Phi;
    public double Psi => state.Psi;
    public double ThetaDot => state.ThetaDot;
    public double PhiDot => CalcPhiDot(state);
    public double PsiDot => CalcPsiDot(state);
    public double Omega3 => pPsi / i3;
    public double Energy => CalcEnergy(state);


    // vector properties

    public Vec3d AxisDirection => new Vec3d {
        X = Math.Sin(Phi) * Math.Sin(Theta),
        Y = -Math.Cos(Phi) * Math.Sin(Theta),
        Z = Math.Cos(Theta),
    };
    public Vec3d LineOfNodes => new Vec3d {
        X = Math.Cos(Phi),
        Y = Math.Sin(Phi),
        Z = 0,
    };

    public Vec3d AngularVelocity => Vec3d.UnitZ*PhiDot + LineOfNodes*ThetaDot + AxisDirection*PsiDot;
    public Vec3d AngularMomentum => AxisDirection*i3*Omega3 + (AngularVelocity-AxisDirection*Omega3)*i1;
    public Vec3d Torque => Vec3d.Cross(AxisDirection, Vec3d.UnitZ) * (-1)*m*g*l ;


    // constructor class
    public GyroscopeModel(double mass, double leverArm, double radius, double gravity = 9.81) {
        m = mass;                                           // mass
        g = gravity;                                        // gravity 
        l = leverArm;                                       // distance l between pivot and mass
        i1 = 1.0/4*mass*Math.Pow(radius, 2) + mass*NPow(l, 2);
        i3 = 1.0/2*mass*Math.Pow(radius, 2);
    }


    private double NPow(double x, int n) {
        if (n == 0) {
            return 1;
        } else {
            double prod = x;
            for (int i = 1; i < n; i++) {
                prod *= x;
            }
            return prod;
        }
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
        double psiDot = Omega3 - phiDot*cosTheta;
        return psiDot;
    }

    private double CalcThetaDotDot(in TopState y) {
        double theta = y.Theta;
        double sinTheta = Math.Sin(theta);
        double cosTheta = Math.Cos(theta);

        double phiDot = CalcPhiDot(y);
        double psiDot = CalcPsiDot(y);
        double thetaDotDot = NPow(phiDot, 2)*sinTheta*cosTheta - i3/i1*Omega3*phiDot*sinTheta + m*g*l*sinTheta/i1;
        return thetaDotDot;
    }

    // helper function for the energy of the system state y 
    private double CalcEnergy(in TopState y) {
        // state of the system
        double theta = y.Theta;
        double thetaDot = y.ThetaDot;
        double phiDot = CalcPhiDot(y);
        double psiDot = CalcPsiDot(y);
        // calculate energy
        double e_kin = (i1*(NPow(phiDot*Math.Sin(theta), 2) + NPow(thetaDot, 2)) + i3*NPow(Omega3, 2)) / 2;
        double e_pot = m*g*l*Math.Cos(theta);
        return e_kin + e_pot;
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
    public void EulerStep(double dt) {
        Derivatives(state, out TopState f);
        state += f * dt;
    }


    // // verlet integrator: integrates TopState y
    // public void VerletStep(double dt) {
    //     Derivatives(state, out TopState a1);

    //     // updating the angles with a1
    //     state.Theta += a1.Theta * dt + a1.Theta * NPow(dt, 2) / 2;
    //     state.ThetaDot += a1.ThetaDot * dt + a1.ThetaDot * NPow(dt, 2) / 2;

    //     Derivatives(state, out TopState a2);
        
    // }


    // rk4 integrator: integrates TopState y
    public void RK4Step(double dt) {
        TopState support;
        // calculation k1 = f(t, y) * dt and support = y + k1/2
        Derivatives(state, out TopState k1);
        k1 *= dt;
        support = state + k1 * 0.5;
        // calculation k2 = f(t+dt/2, y+k1/2) * dt and support = y + k2/2
        Derivatives(support, out TopState k2);
        k2 *= dt;
        support= state + k2 * 0.5;
        // calculation k3 = f(t+dt/2, y+k2/2) * dt and support = y + k3
        Derivatives(support, out TopState k3);
        k3 *= dt;
        support = state + k3;
        // calculation k4 = f(t+dt, y+k2) * dt and y_(i+1)
        Derivatives(support, out TopState k4);
        k4 *= dt;
        state += (k1 + k2*2 + k3*2 + k4) * (1.0/6); 
    }
}