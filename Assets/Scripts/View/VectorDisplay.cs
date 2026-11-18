using UnityEngine;


// needs a controller on the same GameObject (GyroscopeController or ModuleGyroscopeController)
public class VectorDisplay : MonoBehaviour {
    [Header("Arrows")]
    [SerializeField] private ArrowBase arrowL;
    [SerializeField] private ArrowBase arrowOmega;
    [SerializeField] private ArrowBase arrowD;

    [Header("Visibility")]
    [SerializeField] private bool showL = true;
    [SerializeField] private bool showOmega = true;
    [SerializeField] private bool showD = true;
    public bool ShowL {get => showL; set => showL = value;}
    public bool ShowOmega {get => showOmega; set => showOmega = value;}
    public bool ShowD {get => showD; set => showD = value;}

    [Header("Scale (metres of arrow per physical unit)")]
    [SerializeField] private float scaleL = 11f;                            // m per kg·m²/s
    [SerializeField] private float scaleOmega = 4e-4f;                      // m per rad/s
    [SerializeField] private float scaleD = 2.5f;                           // m per N·m

    [Header("Anchor")]
    // m along the figure axis from the pivot where the arrows start; 0 = at the pivot (original, module). The lecture
    // gyroscope draws them from the hub on top of its long rod, where they would otherwise run hidden inside it.
    // Taken from the model (not from a transform), so it never lags the pose by a frame.
    [SerializeField] private float anchorHeight = 0f;

    private IGyroscopeModelSource controller;


    // the module switches between apparatus of different size
    public void SetScales(float l, float omega, float d) {
        scaleL = l;
        scaleOmega = omega;
        scaleD = d;
    }


    // general function to show or hide vectors
    private void DrawOrHide(ArrowBase arrow, bool show, Vector3 vector, Vector3 pivot) {
        if (show) {
            arrow.Draw(pivot, vector);
        } else {
            arrow.Draw(pivot, Vector3.zero);
        }
    }


    void Awake() {
        controller = GetComponent<IGyroscopeModelSource>();                 // GetComponent also finds components by interface
    }


    void Update() {
        GyroscopeModel model = controller.Model;
        // no model (controllers Start() has not run yet)
        if (model == null) {
            return;
        }
        Vector3 pivot = transform.position;                                 // Gyroscope sits exactly at the pivot point
        if (anchorHeight != 0f) pivot += anchorHeight * PhysicsToUnity.Vector(model.AxisDirection);

        // draw the three arrows according to their respective boolean
        DrawOrHide(arrowL, showL, scaleL * PhysicsToUnity.Vector(model.AngularMomentum), pivot);
        DrawOrHide(arrowOmega, showOmega, scaleOmega * PhysicsToUnity.Vector(model.AngularVelocity), pivot);
        DrawOrHide(arrowD, showD, scaleD * PhysicsToUnity.Vector(model.Torque), pivot);
    }
}