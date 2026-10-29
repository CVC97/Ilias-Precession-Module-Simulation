using UnityEngine;


// The three cones of the force-free (balanced) gyroscope, apex at the pivot, all with the same slant length (longer
// than the precession cone, see length):
//   Nutationskegel  around L⃗, through the figure axis: the path of the axle around the fixed L⃗
//   Rastpolkegel    around L⃗, through ω⃗: fixed in space, the path of the instantaneous axis of rotation
//   Gangpolkegel    around the figure axis, through ω⃗: fixed in the body, rolls on the Rastpolkegel; ω⃗ is the line
//                   where the two touch
// Without torque L⃗ is constant: the cones around L⃗ stand still, the Gangpolkegel rolls around. With torque they would
// only be momentary shapes, so ModuleHelpers shows them only for the balanced gyroscope.
// Before a push all angles are 0 (L⃗, ω⃗ and the figure axis on one line): the cones are hidden.
public class NutationCones : MonoBehaviour {
    [SerializeField] private ModuleGyroscopeController controller;
    [SerializeField] private HelperCone nutationCone;
    [SerializeField] private HelperCone spaceCone;                              // Rastpolkegel
    [SerializeField] private HelperCone bodyCone;                               // Gangpolkegel
    [SerializeField] private float minAngleDeg = 0.5f;                          // below: a line, not a cone
    [SerializeField] private float length = 0.30f;                              // m, apex to rim; only on the lab gyroscope
                                                                                // (balanced): well beyond the wheel (centre
                                                                                // 0.12 m, radius 0.12 m), which hides cones inside it


    void LateUpdate() {
        GyroscopeModel model = controller.Model;
        if (model == null) return;

        // directions only; the mapping to Unity is a rotation, so the angles stay the same
        Vector3 l = PhysicsToUnity.Vector(model.AngularMomentum).normalized;
        Vector3 omega = PhysicsToUnity.Vector(model.AngularVelocity).normalized;   // ω⃗·L⃗ = 2 E_kin > 0: on L⃗'s side
        Vector3 axis = PhysicsToUnity.Vector(model.AxisDirection);
        if (Vector3.Dot(axis, l) < 0) axis = -axis;                             // the half on L⃗'s side (lab: the wheel)

        Vector3 pivot = controller.transform.position;
        Place(nutationCone, pivot, l, Vector3.Angle(l, axis));
        Place(spaceCone, pivot, l, Vector3.Angle(l, omega));
        Place(bodyCone, pivot, axis, Vector3.Angle(axis, omega));
    }


    // cone around the given axis with the given half angle (deg)
    private void Place(HelperCone cone, Vector3 apex, Vector3 axis, float halfAngleDeg) {
        if (!cone.gameObject.activeInHierarchy) return;                         // switched off
        cone.SetVisible(halfAngleDeg >= minAngleDeg);
        float a = halfAngleDeg * Mathf.Deg2Rad;
        cone.Set(apex, axis, length * Mathf.Cos(a), length * Mathf.Sin(a));
    }
}
