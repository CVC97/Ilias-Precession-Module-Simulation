using UnityEngine;


// Open cone (no base) with a rim line, for the helper cones (precession now, nutation and pole cones later): apex at
// this transform, axis along its local up. One unit mesh (rim radius 1 at height 1) is built once and sized by the
// transform's scale every frame, which is cheaper than new geometry per frame. The material shows both faces.
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class HelperCone : MonoBehaviour {
    [SerializeField] private LineRenderer rim;                                  // on a child (one renderer per object), local space

    private const int Segments = 96;


    void Awake() {
        var vertices = new Vector3[Segments + 1];                               // 0 = apex, then the rim
        var triangles = new int[3 * Segments];
        var rimPoints = new Vector3[Segments];
        for (int i = 0; i < Segments; i++) {
            float a = 2 * Mathf.PI * i / Segments;
            rimPoints[i] = new Vector3(Mathf.Cos(a), 1f, Mathf.Sin(a));
            vertices[i + 1] = rimPoints[i];
            triangles[3 * i] = 0;
            triangles[3 * i + 1] = 1 + (i + 1) % Segments;
            triangles[3 * i + 2] = 1 + i;
        }
        var mesh = new Mesh { name = "HelperCone", vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals();
        GetComponent<MeshFilter>().sharedMesh = mesh;

        rim.useWorldSpace = false;
        rim.loop = true;
        rim.positionCount = Segments;
        rim.SetPositions(rimPoints);
    }


    // apex, unit axis, height along the axis (negative: the cone opens the other way), rim radius; all in metres
    public void Set(Vector3 apex, Vector3 axis, float height, float radius) {
        transform.SetPositionAndRotation(apex, Quaternion.FromToRotation(Vector3.up, axis));
        if (Mathf.Abs(height) < 1e-4f) height = 1e-4f;                          // flat cone at θ = 90°: keep the scale invertible
        transform.localScale = new Vector3(radius, height, radius);
    }
}
