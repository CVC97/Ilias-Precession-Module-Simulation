using UnityEngine;


// What the controller needs from an apparatus: both are symmetric tops with one fixed point, so GyroscopeModel
// describes both; only these numbers differ. Implemented by LabGyroscope and SimpleTop.
// Convention: the model's figure axis points from the pivot to the side where the torque pulls down.
public abstract class ApparatusParameters : ScriptableObject {
    public abstract double I3 { get; }                              // kg·m², about the figure axis
    public abstract double I1(double addOnMass);                    // kg·m², about a horizontal axis through the pivot
    public abstract double TorqueConst(double addOnMass);           // N·m, D = k sinθ
    public abstract double FrictionCoeff { get; }                   // N·m·s, bearing torque −c ψ̇ (when switched on)

    // sign of ω3 for which L points from the pivot through the wheel (textbook picture)
    public abstract int SpinSign { get; }
}
