using UnityEngine;


[RequireComponent(typeof(LineRenderer))]
public class VectorArrow : MonoBehaviour {
    [SerializeField] private LineRenderer head;                         // drag the child "Head" in here
    [SerializeField] private float shaftWidth = 0.0015f;                // m
    [SerializeField] private float headWidth  = 0.005f;                 // m (base of the head)
    [SerializeField] private float headLength = 0.01f;                  // m

    private LineRenderer shaft;


    void Awake() {                                                      // Awake() runs before Start()
        shaft = GetComponent<LineRenderer>();                           // the LineRenderer on this same GameObject
        // shaft
        shaft.positionCount = 2;
        shaft.startWidth = shaftWidth;
        shaft.endWidth = shaftWidth;
        // head
        head.positionCount = 2;
        head.startWidth = headWidth;
        head.endWidth = 0f;
        head.sharedMaterial = shaft.sharedMaterial;                     // sharedMaterial: actual material, material: COPY per object
    }


    public void Draw(Vector3 origin, Vector3 vector) { 
        float length = vector.magnitude;
        // tiny vectors
        if (length < 1e-5f) {
            shaft.enabled = false;
            head.enabled = false;
            return;
        }
        shaft.enabled = true;
        head.enabled = true;

        Vector3 dir = vector / length;
        float hl = Mathf.Min(headLength, 0.5f * length);                // reduce head length for short vectors
        Vector3 shaftEnd = origin + dir * (length - hl);                // shaft end by substracting head length
        // set shaft position
        shaft.SetPosition(0, origin);                                   // using passed origin
        shaft.SetPosition(1, shaftEnd);                                 // and calculated shaftEnd
        // set head postion
        head.SetPosition(0, shaftEnd);
        head.SetPosition(1, origin + vector);
    }


    [ContextMenu("Test Draw")]
    private void TestDraw() {
        Awake();                                                        // in edit mode Awake() hasn't run
        Draw(transform.position, new Vector3(0.05f, 0.1f, 0f));
    }

}