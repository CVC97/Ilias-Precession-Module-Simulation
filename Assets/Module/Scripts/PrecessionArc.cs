using System;
using TMPro;
using UnityEngine;


// ω_P as a curved arrow around the vertical axis above the pivot: shows the sense of the precession (the page shows
// the value; the module texts use ω_P as a rate, not as a vector). Sits on a non-rotating object above the pivot,
// at a height that depends on the apparatus (the simple top's disk can stand almost upright above the pivot).
[DefaultExecutionOrder(100)]
public class PrecessionArc : MonoBehaviour {
    [SerializeField] private ModuleGyroscopeController controller;
    [SerializeField] private LineRenderer arc;                                  // local space of this object
    [SerializeField] private Transform head;                                    // cone, its local up is where it points
    [SerializeField] private TextMeshPro label;                                 // optional; the ω⃗_P vector carries the label now
    [SerializeField] private float radius = 0.07f;                              // m
    [SerializeField] private float sweepDeg = 270f;
    [SerializeField] private float minRate = 0.01f;                             // rad/s; slower precession: hidden

    private const int Points = 48;
    private float drawnSense;                                                   // +1 / −1, 0 = not drawn yet
    private Transform cam;


    void Awake() {
        cam = Camera.main.transform;
    }


    void LateUpdate() {
        GyroscopeModel model = controller.Model;
        bool visible = model != null && Math.Abs(model.PhiDot) > minRate;
        transform.position = controller.transform.position + Vector3.up * controller.ActiveRig.PrecessionArcHeight;
        arc.enabled = visible;
        head.gameObject.SetActive(visible);
        if (label != null) label.enabled = visible;
        if (!visible) {
            return;
        }

        // Unity's yaw angle is −φ (left-handed, see PhysicsToUnity), so the axle turns with the sign of −φ̇ about up
        float sense = -Mathf.Sign((float)model.PhiDot);
        if (sense != drawnSense) {
            Build(sense);
            drawnSense = sense;
        }
        if (label != null) label.transform.rotation = cam.rotation;
    }


    // arc in the horizontal plane; Quaternion.AngleAxis(α, up) moves (1,0,0) to (cos α, 0, −sin α)
    private void Build(float sense) {
        arc.useWorldSpace = false;
        arc.positionCount = Points;
        float sweep = sense * sweepDeg * Mathf.Deg2Rad;
        for (int i = 0; i < Points; i++) {
            float a = sweep * i / (Points - 1);
            arc.SetPosition(i, new Vector3(radius * Mathf.Cos(a), 0f, -radius * Mathf.Sin(a)));
        }
        Vector3 tangent = sense * new Vector3(-Mathf.Sin(sweep), 0f, -Mathf.Cos(sweep));
        head.localPosition = new Vector3(radius * Mathf.Cos(sweep), 0f, -radius * Mathf.Sin(sweep));
        head.localRotation = Quaternion.FromToRotation(Vector3.up, tangent);
        if (label != null) label.transform.localPosition = new Vector3(radius + 0.035f, 0.02f, 0f);   // beside the start of the arc
    }
}
