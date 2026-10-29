using UnityEngine;


// Parameters of the lab gyroscope (Versuch 4), rounded generously from one group's measurements; only the order of
// magnitude matters (learning_migration.md, A3). The asset lives at Assets/Module/LabGyroscope.asset; more can be
// made with Assets → Create → Module → Lab Gyroscope.
// Axis convention: the model's figure axis points from the pivot to the counterweight side, where the add-on weight
// hangs in the notch; the wheel sits on the opposite side. So an add-on weight gives a positive torque constant.
[CreateAssetMenu(fileName = "LabGyroscope", menuName = "Module/Lab Gyroscope")]
public class LabGyroscope : ApparatusParameters {
    [Header("Wheel")]
    [SerializeField] private float wheelMass = 1.3f;                        // kg (measured 1.324)
    [SerializeField] private float wheelRadius = 0.12f;                     // m (0.1225)
    [SerializeField] private float wheelDistance = 0.12f;                   // m, wheel centre from the pivot

    [Header("Counterweight: position follows from the balance, as in step 3 of the Durchführung")]
    [SerializeField] private float counterweightMass = 0.9f;                // kg (0.938)

    [Header("Add-on weight")]
    [SerializeField] private float notchDistance = 0.27f;                   // m, notch from the pivot (0.272)

    [Header("Bearing friction (when switched on)")]
    [SerializeField] private float spinDecayTime = 120f;                    // s, time constant of the spin ω_R

    [SerializeField] private float gravity = 9.81f;                         // m/s²


    public float WheelRadius => wheelRadius;
    public float WheelDistance => wheelDistance;
    public float NotchDistance => notchDistance;
    public float CounterweightDistance => wheelMass * wheelDistance / counterweightMass;   // m_w d_w = m_c d_c

    // wheel as a thin disk; the measured value (pendulum) was 0.011 kg·m²
    public override double I3 => 0.5 * wheelMass * wheelRadius * wheelRadius;

    // about a horizontal axis through the pivot: wheel (disk about its diameter + Steiner), counterweight and
    // add-on weight as point masses; the axle is neglected
    public override double I1(double addOnMass) {
        double wheel = 0.25 * wheelMass * wheelRadius * wheelRadius + wheelMass * wheelDistance * wheelDistance;
        double counterweight = counterweightMass * CounterweightDistance * CounterweightDistance;
        return wheel + counterweight + addOnMass * notchDistance * notchDistance;
    }

    // the balanced gyroscope only feels the add-on weight: D = m_z g r sinθ
    public override double TorqueConst(double addOnMass) => addOnMass * gravity * notchDistance;

    // friction torque −c ψ̇ with c = I3/τ: for a horizontal axle without precession ω_R decays as e^(−t/τ)
    public override double FrictionCoeff => I3 / spinDecayTime;

    // the wheel sits on the negative side of the figure axis
    public override int SpinSign => -1;
}
