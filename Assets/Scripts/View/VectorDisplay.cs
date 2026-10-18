using UnityEngine;


[RequireComponent(typeof(GyroscopeController))]
public class VectorDisplay : MonoBehaviour {
    [Header("Arrows")]
    [SerializeField] private VectorArrow arrowL;
    [SerializeField] private VectorArrow arrowOmega;
    [SerializeField] private VectorArrow arrowD;

    [Header("Visibility")]
    [SerializeField] private bool showL = true;
    [SerializeField] private bool showOmega = true;
    [SerializeField] private bool showD = true;

    [Header("Scale (metres of arrow per physical unit)")]
    [SerializeField] private float scaleL = 11f;                            // m per kg·m²/s
    [SerializeField] private float scaleOmega = 4e-4f;                      // m per rad/s
    [SerializeField] private float scaleD = 1.9f;                           // m per N·m

    private GyroscopeController controller;


    // general function to show or hide vectors
    private void DrawOrHide(VectorArrow arrow, bool show, Vector3 vector, Vector3 pivot) {
        if (show) {
            arrow.Draw(pivot, vector);
        } else {
            arrow.Draw(pivot, Vector3.zero);
        }
    }


    void Awake() {
        controller = GetComponent<GyroscopeController>();
    }


    void Update() {
        GyroscopeModel model = controller.Model;
        // no model (controllers Start() has not run yet)
        if (model == null) {
            return;
        }
        Vector3 pivot = transform.position;                                 // Gyroscope sits exactly at the pivot point

        // draw the three arrows according to their respective boolean
        DrawOrHide(arrowL, showL, scaleL * PhysicsToUnity.Vector(model.AngularMomentum), pivot);
        DrawOrHide(arrowOmega, showOmega, scaleOmega * PhysicsToUnity.Vector(model.AngularVelocity), pivot);
        DrawOrHide(arrowD, showD, scaleD * PhysicsToUnity.Vector(model.Torque), pivot);
    }
}