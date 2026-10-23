using UnityEngine;


public class VectorArrow3D : ArrowBase {
    [SerializeField] private Transform shaft;
    [SerializeField] private Transform head;
    [SerializeField] private float shaftDiameter = 0.002f;                                  // m
    [SerializeField] private float headDiameter  = 0.006f;                                  // m
    [SerializeField] private float headLength    = 0.012f;                                  // m

    private MeshRenderer shaftRenderer, headRenderer;

    void Awake() {
        // get the MeshRenderer of shaft and head (GetComponent on shaft/head)
        shaftRenderer = shaft.GetComponent<MeshRenderer>();
        headRenderer = head.GetComponent<MeshRenderer>();
        // head uses the same material as the shaft (sharedMaterial)
        headRenderer.sharedMaterial = shaftRenderer.sharedMaterial;                         // sharedMaterial: actual material, material: COPY per object
    }

    public override void Draw(Vector3 origin, Vector3 vector) {
        float length = vector.magnitude;
        // tiny vectors
        if (length < 1e-5f) {
            shaftRenderer.enabled = false;
            headRenderer.enabled = false;
            return;
        }
        shaftRenderer.enabled = true;
        headRenderer.enabled = true;

        Vector3 dir = vector / length;
        float hl = Mathf.Min(headLength, 0.5f * length);                                    // reduce head length for short vectors
        float shaftLen = length - hl;                                                   

        transform.SetPositionAndRotation(origin, Quaternion.FromToRotation(Vector3.up, dir));

        // local coordinates
        shaft.localPosition = new Vector3(0, shaftLen / 2, 0);
        head.localPosition = new Vector3(0, shaftLen, 0);
        shaft.localScale = new Vector3(shaftDiameter, shaftLen / 2, shaftDiameter);
        head.localScale = new Vector3(headDiameter, hl, headDiameter);
    }
    

    [ContextMenu("Test Draw")]
    private void TestDraw() {
        Awake();
        Draw(transform.position, new Vector3(0.05f, 0.1f, 0f));
    }
}
