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
    [SerializeField] private float precessionArcHeight = 0.16f;                 // m above the pivot, clear of the rotor
    [SerializeField] private float precessionScale = 0.6f;                      // m per rad/s for the ω_P vector
    [SerializeField] private float precessionConeLength = 0.2f;                 // m, pivot to the axle end on the side L⃗ points to

    public ApparatusParameters Parameters => parameters;
    public Transform Rotor => rotor;
    public TrailRenderer Trail => trail;
    public float MinTheta0Deg => minTheta0Deg;
    public float MaxTheta0Deg => maxTheta0Deg;
    public Vector3 VectorScales => vectorScales;
    public float PrecessionArcHeight => precessionArcHeight;
    public float PrecessionScale => precessionScale;
    public float PrecessionConeLength => precessionConeLength;


    public void Show(bool visible) {
        gameObject.SetActive(visible);
        stand.SetActive(visible);
    }
}
