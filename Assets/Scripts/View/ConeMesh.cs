using System.Collections.Generic; 
using UnityEngine;


[ExecuteAlways]                                                                 // also runs in edit mode → visible in the Scene view
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ConeMesh : MonoBehaviour {
    [SerializeField, Range(3, 64)] private int segments = 64;
    [SerializeField] private float radius = 0.5f;
    [SerializeField] private float height = 1f;

    void OnEnable()   { Generate(); }
    void OnValidate() { Generate(); }                                                       // regenerate when values change in the Inspector


    private void Generate() {
        var vertices  = new List<Vector3>();
        var normals   = new List<Vector3>();
        var triangles = new List<int>();

        // side surface: one triangle per segment
        Vector3 centre = new Vector3(0, 0, 0);                                              // centre of the cone
        Vector3 tip = new Vector3(0, height, 0);                                            // tip of the cone
        Vector3 baseNormal = new Vector3(0, -1, 0);                                         // normal of the base (pointing downwards)
        for (int i = 0; i < segments; i++) {
            // base circle angles
            float a0 = 2*Mathf.PI/segments * i;                                             // base circle: starting angle
            float a1 = 2*Mathf.PI/segments * (i+1);                                         // base circle: end angle
            float am = (a0 + a1) / 2;                                                       // base circle: middle angle
            // triangle points: v0 (base, a0), v1 (base, a1), tip
            Vector3 v0 = new Vector3(radius*Mathf.Cos(a0), 0, radius*Mathf.Sin(a0));        // starting angle point
            Vector3 v1 = new Vector3(radius*Mathf.Cos(a1), 0, radius*Mathf.Sin(a1));        // end angle point
            int j = vertices.Count;                                                         // index of the next vertex
            vertices.Add(v0);
            vertices.Add(v1);
            vertices.Add(tip);
            // normals to cone side for the 3 above angles: n(a0), n(a1), n(am)
            Vector3 na0 = new Vector3(height*Mathf.Cos(a0), radius, height*Mathf.Sin(a0)).normalized;
            Vector3 na1 = new Vector3(height*Mathf.Cos(a1), radius, height*Mathf.Sin(a1)).normalized;
            Vector3 nam = new Vector3(height*Mathf.Cos(am), radius, height*Mathf.Sin(am)).normalized;
            normals.Add(na0);
            normals.Add(na1);
            normals.Add(nam);
            // three indices form triangles to form surface: order v0 → tip → v1
            triangles.Add(j);                                                               // v0 index
            triangles.Add(j+2);                                                             // v1 index
            triangles.Add(j+1);                                                             // tip index
        }

        // base surface: one triangle per segment
        for (int i = 0; i < segments; i++) {
            // base circle angles
            float a0 = 2*Mathf.PI/segments * i;                                             // base circle: starting angle
            float a1 = 2*Mathf.PI/segments * (i+1);                                         // base circle: end angle
            // triangle points: v0 (base, a0), v1 (base, a1), centre
            Vector3 v0 = new Vector3(radius*Mathf.Cos(a0), 0, radius*Mathf.Sin(a0));        // starting angle point
            Vector3 v1 = new Vector3(radius*Mathf.Cos(a1), 0, radius*Mathf.Sin(a1));        // end angle point
            int j = vertices.Count;                                                         // index of the next vertex
            vertices.Add(centre);
            vertices.Add(v0);
            vertices.Add(v1);
            // base normal
            normals.Add(baseNormal);
            normals.Add(baseNormal);
            normals.Add(baseNormal);
            // three indices form triangles to form surface: order v0 → base → v1
            triangles.Add(j);                                                               // centre index
            triangles.Add(j+1);                                                             // v0 index
            triangles.Add(j+2);                                                             // v1 index
        }


        var mesh = new Mesh { name = "Cone", hideFlags = HideFlags.DontSave };

        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        GetComponent<MeshFilter>().sharedMesh = mesh;
    }
}
