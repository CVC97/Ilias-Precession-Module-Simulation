using UnityEngine;


[RequireComponent(typeof(LineRenderer))]
public class VectorArrow : MonoBehaviour {
    [SerializeField] private LineRenderer head;                         // drag the child "Head" in here
    [SerializeField] private float shaftWidth = 0.0015f;                // m
    [SerializeField] private float headWidth  = 0.005f;                 // m (base of the head)
    [SerializeField] private float headLength = 0.01f;                  // m

    private LineRenderer shaft;

    void Awake() {                                                      // Awake() runs before Start()
        line = GetComponent<LineRenderer>();                            
    }

    public void Draw(Vector3 origin, Vector3 vector) { 

    }

}