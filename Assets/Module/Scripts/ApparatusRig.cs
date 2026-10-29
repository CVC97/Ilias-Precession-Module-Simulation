using UnityEngine;


// One apparatus as seen in the scene: its parameters, the parts the controller moves, its stand, and the settings
// that depend on its size. Sits on the rig root under the Gyroscope object (LabRig, TopRig).
public class ApparatusRig : MonoBehaviour {
    [SerializeField] private ApparatusParameters parameters;
    [SerializeField] private Transform rotor;                                   // spins about its local up axis
    [SerializeField] private TrailRenderer trail;
    [SerializeField] private GameObject stand;                                  // separate root object, doesn't move

    [Header("Limits and display")]
    [SerializeField] private float minTheta0Deg = 60f;                          // the wheel must not hit the stand
    [SerializeField] private float maxTheta0Deg = 120f;
    [SerializeField] private Vector3 vectorScales = new Vector3(0.9f, 0.0053f, 2.5f);  // m per unit: L, ω, D

    public ApparatusParameters Parameters => parameters;
    public Transform Rotor => rotor;
    public TrailRenderer Trail => trail;
    public float MinTheta0Deg => minTheta0Deg;
    public float MaxTheta0Deg => maxTheta0Deg;
    public Vector3 VectorScales => vectorScales;


    public void Show(bool visible) {
        gameObject.SetActive(visible);
        stand.SetActive(visible);
    }
}
