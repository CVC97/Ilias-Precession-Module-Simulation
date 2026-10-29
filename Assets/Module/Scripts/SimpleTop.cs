using UnityEngine;


// The simple top for the fundamentals pages (p2, p4): a disk on an axle whose lower end rests on a tower, like the
// original simulation, but at the size of the lab wheel. Proportions as in the original (lever arm ≈ 1.7 × radius).
// The asset lives at Assets/Module/SimpleTop.asset.
// Axis convention: the figure axis points from the pivot to the disk, so its own weight gives k = m g l > 0.
[CreateAssetMenu(fileName = "SimpleTop", menuName = "Module/Simple Top")]
public class SimpleTop : ApparatusParameters {
    [Header("Disk")]
    [SerializeField] private float diskMass = 1.0f;                         // kg
    [SerializeField] private float diskRadius = 0.10f;                      // m
    [SerializeField] private float leverArm = 0.167f;                       // m, disk centre from the pivot

    [Header("Bearing friction (when switched on)")]
    [SerializeField] private float spinDecayTime = 120f;                    // s

    [Header("Spin")]
    [SerializeField] private float maxRpm = 3000f;

    [SerializeField] private float gravity = 9.81f;                         // m/s²


    public float DiskRadius => diskRadius;
    public float LeverArm => leverArm;

    public override double I3 => 0.5 * diskMass * diskRadius * diskRadius;

    // disk about its diameter + Steiner; the add-on weight doesn't exist here
    public override double I1(double addOnMass) => 0.25 * diskMass * diskRadius * diskRadius + diskMass * leverArm * leverArm;

    // heavy top: its own weight, D = m g l sinθ
    public override double TorqueConst(double addOnMass) => diskMass * gravity * leverArm;

    public override double FrictionCoeff => I3 / spinDecayTime;

    // the disk sits on the positive side of the figure axis
    public override int SpinSign => 1;

    public override float MaxRpm => maxRpm;
}
