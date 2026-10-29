using System;
using TMPro;
using UnityEngine;


// Precession as the cone the figure axis sweeps around the vertical: apex at the pivot, on the side L⃗ points to (lab:
// through the wheel, not the counterweight), rim at that axle end, so its size belongs to the apparatus (not to an
// arrow scale or the spin rate). Drawn at the mean θ over a few nutation periods: it stands still, and nutation shows
// as the axle wobbling around it.
// The surface fades out as the cone flattens towards θ = 90° (there it would be a disk cutting through the wheel);
// the rim stays. The ω_P label sits beyond the rim on the camera's right, so it never covers the apparatus.
[DefaultExecutionOrder(100)]                                                    // after ModuleHelpers and the orbit camera
public class PrecessionDisplay : MonoBehaviour {
    [SerializeField] private ModuleGyroscopeController controller;
    [SerializeField] private HelperCone cone;
    [SerializeField] private TextMeshPro label;
    [SerializeField] private float smoothingSeconds = 2f;                       // longer than a nutation period
    [SerializeField] private float fullSurfaceTheta = 60f;                      // deg; from here towards 90° the surface fades
    [SerializeField] private float labelOffset = 0.05f;                         // m beyond the rim

    private GyroscopeModel drawnModel;                                          // a new model = restart or other apparatus
    private double meanTheta;
    private MeshRenderer surface;
    private Color surfaceColor;
    private MaterialPropertyBlock block;                                        // per-renderer colour, the material asset stays
    private Transform cam;


    void Awake() {
        surface = cone.GetComponent<MeshRenderer>();
        surfaceColor = surface.sharedMaterial.GetColor("_BaseColor");
        block = new MaterialPropertyBlock();
        cam = Camera.main.transform;
    }


    void LateUpdate() {
        GyroscopeModel model = controller.Model;
        if (model == null) return;

        // exponential moving average (low-pass filter); a new model starts at its own θ
        // controller.Theta is the angle of L⃗ (wheel side) from the vertical
        if (model != drawnModel) {
            meanTheta = controller.Theta;
            drawnModel = model;
        } else {
            meanTheta += (controller.Theta - meanTheta) * (1 - Math.Exp(-Time.deltaTime / smoothingSeconds));
        }

        Vector3 pivot = controller.transform.position;
        float length = controller.ActiveRig.PrecessionConeLength;
        float height = length * (float)Math.Cos(meanTheta);                     // negative: the cone opens downwards
        float radius = length * (float)Math.Sin(meanTheta);
        cone.Set(pivot, Vector3.up, height, radius);

        float steepness = Mathf.Abs(height) / length / Mathf.Cos(fullSurfaceTheta * Mathf.Deg2Rad);
        Color color = surfaceColor;
        color.a *= Mathf.SmoothStep(0f, 1f, steepness);
        block.SetColor("_BaseColor", color);
        surface.SetPropertyBlock(block);

        label.enabled = cone.gameObject.activeInHierarchy;
        if (label.enabled) {
            Vector3 right = Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized;
            label.transform.position = pivot + Vector3.up * height + right * (radius + labelOffset);
            label.transform.rotation = cam.rotation;                            // billboard
        }
    }
}
