using UnityEngine;


// The bearing block on top of the stand: turns with the precession φ, but doesn't tilt with θ (the axle tilts in it).
public class PivotHead : MonoBehaviour {
    [SerializeField] private ModuleGyroscopeController controller;


    void LateUpdate() {
        GyroscopeModel model = controller.Model;
        if (model == null) {
            return;
        }
        transform.localRotation = Quaternion.AngleAxis(PhysicsToUnity.Angle(model.Phi), Vector3.up);
    }
}
