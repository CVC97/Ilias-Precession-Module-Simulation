using UnityEngine;


// The add-on weight on the hub of the lecture gyroscope: a steel slug around the spike on top of the rod, as tall as
// its mass needs (h = m / (ρ π r²)); hidden without weight. It sits on the axis, so it needs no spin of its own.
// The cylinder mesh spans y −1 … 1 (Unity's primitive and SmoothCylinder), so scale.y is half the height.
public class LectureAddOnWeight : MonoBehaviour {
    [SerializeField] private ModuleGyroscopeController controller;
    [SerializeField] private Transform slug;                                    // cylinder child, bottom at this object's origin
    [SerializeField] private float radius = 0.03f;                              // m
    [SerializeField] private float density = 7850f;                             // kg/m³, steel


    // LateUpdate: after the controller's Update (a slider change shows in the same frame)
    void LateUpdate() {
        float mass = controller.AddOnMassGrams / 1000f;
        bool visible = mass > 0.0005f;
        slug.gameObject.SetActive(visible);
        if (!visible) return;

        float height = mass / (density * Mathf.PI * radius * radius);
        slug.localPosition = new Vector3(0f, height / 2, 0f);
        slug.localScale = new Vector3(2 * radius, height / 2, 2 * radius);
    }
}
