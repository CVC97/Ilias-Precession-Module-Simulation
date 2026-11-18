using UnityEngine;


// The lecture-hall gyroscope (Vorlesungskreisel): a thin, light disk on top of a long rod, the rod ending in a heavy
// tripod with a round plate at its feet. Disk, rod, tripod and plate are one rigid body that spins about the rod; the
// apex of the tripod rests on the tip of a thin fixed pin. The heavy plate hangs below the tip, so the bare top is
// bottom-heavy: it hangs (stable even without spin) and precesses the other way round than a top-heavy one. An
// add-on weight on the hub at the top of the rod lifts the centre of mass: at BalanceMass the top is force-free, above
// it top-heavy. Numbers are estimates (nobody measured the lecture apparatus); the asset lives at
// Assets/Lecture/LectureGyroscope.asset.
// Axis convention: the figure axis points from the tip along the rod to the disk (SpinSign +1, L⃗ points to the disk).
// Heights are measured from the tip along that axis, so parts below it have negative heights. k = g Σ m z: positive
// pulls the rod down (top-heavy), negative pulls it back up (hanging).
[CreateAssetMenu(fileName = "LectureGyroscope", menuName = "Lecture/Lecture Gyroscope")]
public class LectureGyroscope : ApparatusParameters {
    [Header("Disk (thin, light, carries the picture)")]
    [SerializeField] private float diskMass = 0.15f;                        // kg
    [SerializeField] private float diskRadius = 0.20f;                      // m
    [SerializeField] private float diskHeight = 0.75f;                      // m above the tip; the hub with the add-on weight sits here too

    [Header("Rod (from the tip up to the disk)")]
    [SerializeField] private float rodMass = 0.35f;                         // kg

    [Header("Tripod legs (from the apex at the tip down to the plate)")]
    [SerializeField] private float legsMass = 0.6f;                         // kg, all three
    [SerializeField] private float legsHeight = -0.06f;                     // m, their centre of mass
    [SerializeField] private float legsRadius = 0.07f;                      // m, mean distance from the axis

    [Header("Round plate at the feet (ring, the heavy part)")]
    [SerializeField] private float plateMass = 2.5f;                        // kg
    [SerializeField] private float plateOuterRadius = 0.14f;                // m
    [SerializeField] private float plateInnerRadius = 0.09f;                // m, hole for the pin
    // m; chosen so that 120 g on the hub balance the top exactly (BalanceMass), a round slider step on the page
    [SerializeField] private float plateHeight = -0.1191f;

    [Header("Add-on weight on the hub")]
    // within this distance of BalanceMass the weight counts as fine-tuned: k is exactly 0, so the force-free helpers
    // (nutation cones) appear; half a slider step of the page (10 g)
    [SerializeField] private float balanceSnap = 0.005f;                    // kg

    [Header("Bearing friction at factor 1 (the page's slider scales it)")]
    [SerializeField] private float spinDecayTime = 60f;                     // s

    [Header("Spin")]
    [SerializeField] private float maxRpm = 1000f;

    [SerializeField] private float gravity = 9.81f;                         // m/s²


    public float DiskRadius => diskRadius;
    public float DiskHeight => diskHeight;
    public float PlateHeight => plateHeight;
    public float PlateOuterRadius => plateOuterRadius;
    public float PlateInnerRadius => plateInnerRadius;
    public float LegsRadius => legsRadius;

    // Σ m z of the bare body (kg·m): negative, the plate hangs below the tip
    private double BareMassMoment => diskMass * diskHeight + rodMass * diskHeight / 2 + legsMass * legsHeight
                                     + plateMass * plateHeight;

    // add-on weight (kg) on the hub that puts the centre of mass exactly on the tip
    public double BalanceMass => -BareMassMoment / diskHeight;

    // the add-on weight sits on the axis: no contribution to I3. Disk and ring as thin disks/annuli, legs as point
    // masses at their mean radius.
    public override double I3 => 0.5 * diskMass * diskRadius * diskRadius
                                 + 0.5 * plateMass * (plateOuterRadius * plateOuterRadius + plateInnerRadius * plateInnerRadius)
                                 + legsMass * legsRadius * legsRadius;

    // about a horizontal axis through the tip: each part about its own centre + Steiner; rod as a thin rod from the
    // tip; legs roughly as a ring of point masses; add-on weight as a point mass at the hub
    public override double I1(double addOnMass) {
        double disk = 0.25 * diskMass * diskRadius * diskRadius + diskMass * diskHeight * diskHeight;
        double rod = rodMass * diskHeight * diskHeight / 3;
        double legs = legsMass * (legsHeight * legsHeight + legsRadius * legsRadius / 2);
        double plate = 0.25 * plateMass * (plateOuterRadius * plateOuterRadius + plateInnerRadius * plateInnerRadius)
                       + plateMass * plateHeight * plateHeight;
        return disk + rod + legs + plate + addOnMass * diskHeight * diskHeight;
    }

    // k = g Σ m z = g h (m − m*): negative without weight (hanging), 0 when balanced, positive top-heavy
    public override double TorqueConst(double addOnMass) {
        double excess = addOnMass - BalanceMass;
        if (System.Math.Abs(excess) < balanceSnap) return 0;
        return gravity * diskHeight * excess;
    }

    // friction torque −c ψ̇ with c = I3/τ: without precession the spin decays as e^(−t/τ)
    public override double FrictionCoeff => I3 / spinDecayTime;

    public override int SpinSign => 1;

    public override float MaxRpm => maxRpm;
}
