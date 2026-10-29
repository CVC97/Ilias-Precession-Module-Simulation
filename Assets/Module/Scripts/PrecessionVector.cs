using UnityEngine;


// ω⃗_P = φ̇ ẑ, the angular velocity of the precession. An angular velocity is a free vector (only its line matters),
// so it is drawn on the vertical axis centred on the curved arrow: together they show the right-hand rule, and during
// nutation the vector shrinks into the arc's centre and flips there when φ̇ changes sign. Drawn from the pivot it
// would vanish into the stand rod whenever it points down (always, for the lab gyroscope).
[RequireComponent(typeof(VectorArrow3D))]
public class PrecessionVector : MonoBehaviour {
    [SerializeField] private ModuleGyroscopeController controller;

    private VectorArrow3D arrow;


    void Awake() {
        arrow = GetComponent<VectorArrow3D>();
    }


    void Update() {
        GyroscopeModel model = controller.Model;
        if (model == null) {
            return;
        }
        ApparatusRig rig = controller.ActiveRig;
        Vector3 omegaP = rig.PrecessionScale * PhysicsToUnity.Vector(Vec3d.UnitZ * model.PhiDot);
        Vector3 arcCentre = controller.transform.position + Vector3.up * rig.PrecessionArcHeight;
        arrow.Draw(arcCentre - omegaP / 2, omegaP);
    }
}
