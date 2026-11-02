using TMPro;
using UnityEngine;


// Symbol just beyond the tip of a vector arrow (L⃗, ω⃗, D⃗). Faces the camera and is drawn on top of everything
// (overlay material): it is a helper, not part of the apparatus. Hidden while its arrow is hidden.
// Runs after the orbit camera has moved (DefaultExecutionOrder), so it faces the camera of this frame.
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(TextMeshPro))]
public class VectorLabel : MonoBehaviour {
    [SerializeField] private VectorArrow3D arrow;
    [SerializeField] private float offset = 0.035f;                             // m beyond the tip, along the arrow
    [SerializeField] private float sideOffset = 0f;                             // m beside the tip, across the arrow as seen on screen

    private TextMeshPro text;
    private Transform cam;


    void Awake() {
        text = GetComponent<TextMeshPro>();
        cam = Camera.main.transform;
    }


    void LateUpdate() {
        bool visible = arrow.isActiveAndEnabled && arrow.Visible;               // Visible alone is stale once the arrow is switched off
        text.enabled = visible;
        if (!visible) {
            return;
        }
        Vector3 direction = (arrow.Tip - arrow.transform.position).normalized;
        Vector3 side = Vector3.Cross(direction, cam.forward).normalized;        // zero while looking along the arrow
        transform.position = arrow.Tip + direction * offset + side * sideOffset;
        transform.rotation = cam.rotation;                                      // billboard: parallel to the screen
    }
}
