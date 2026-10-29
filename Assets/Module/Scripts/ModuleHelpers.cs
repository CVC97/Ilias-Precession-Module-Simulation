using UnityEngine;


// Switchboard for everything that helps reading the motion but isn't part of the apparatus: vectors, their labels,
// axes, cones, the trail. The page sets these through its presets (step 5); in the editor the Inspector toggles work
// live, because the flags are applied every frame.
// The flags say what may be shown; for the cones the physics decides as well: the precession cone needs a torque,
// the nutation cones need none (only then is L⃗ fixed and they are more than momentary shapes).
public class ModuleHelpers : MonoBehaviour {
    [SerializeField] private ModuleGyroscopeController controller;
    [SerializeField] private VectorDisplay vectors;
    [SerializeField] private GameObject labels;
    [SerializeField] private GameObject figureAxis;
    [SerializeField] private GameObject verticalAxis;
    [SerializeField] private GameObject precessionCone;                         // cone swept by the figure axis, with its ω_P label
    [SerializeField] private GameObject precessionArc;                          // ω_P as a curved arrow (kept for comparison)
    [SerializeField] private GameObject precessionVector;                       // ω⃗_P (kept for comparison)
    [SerializeField] private GameObject nutationCone;                           // Nutationskegel: figure axis around L⃗
    [SerializeField] private GameObject spaceCone;                              // Rastpolkegel: ω⃗ around L⃗
    [SerializeField] private GameObject bodyCone;                               // Gangpolkegel: ω⃗ around the figure axis

    [Header("Visible")]
    [SerializeField] private bool showL = true;
    [SerializeField] private bool showOmega = true;
    [SerializeField] private bool showD = true;
    [SerializeField] private bool showLabels = true;
    [SerializeField] private bool showFigureAxis = false;
    [SerializeField] private bool showVerticalAxis = false;
    [SerializeField] private bool showPrecessionCone = true;
    [SerializeField] private bool showPrecessionArc = false;
    [SerializeField] private bool showPrecessionVector = false;
    [SerializeField] private bool showNutationCone = true;
    [SerializeField] private bool showSpaceCone = true;
    [SerializeField] private bool showBodyCone = true;
    [SerializeField] private bool showTrail = true;                             // grey trail of the axle end


    public void SetShowL(bool on) => showL = on;
    public void SetShowOmega(bool on) => showOmega = on;
    public void SetShowD(bool on) => showD = on;
    public void SetShowLabels(bool on) => showLabels = on;
    public void SetShowFigureAxis(bool on) => showFigureAxis = on;
    public void SetShowVerticalAxis(bool on) => showVerticalAxis = on;
    public void SetShowPrecessionCone(bool on) => showPrecessionCone = on;
    public void SetShowPrecessionArc(bool on) => showPrecessionArc = on;
    public void SetShowPrecessionVector(bool on) => showPrecessionVector = on;
    public void SetShowNutationCone(bool on) => showNutationCone = on;
    public void SetShowSpaceCone(bool on) => showSpaceCone = on;
    public void SetShowBodyCone(bool on) => showBodyCone = on;
    public void SetShowTrail(bool on) => showTrail = on;


    void LateUpdate() {
        vectors.ShowL = showL;
        vectors.ShowOmega = showOmega;
        vectors.ShowD = showD;
        labels.SetActive(showLabels);
        figureAxis.SetActive(showFigureAxis);
        verticalAxis.SetActive(showVerticalAxis);
        precessionArc.SetActive(showPrecessionArc);
        if (precessionVector != null) precessionVector.SetActive(showPrecessionVector);

        bool forceFree = controller.Model != null && controller.Model.TorqueConst == 0;    // balanced lab gyroscope
        if (precessionCone != null) precessionCone.SetActive(showPrecessionCone && !forceFree);
        if (nutationCone != null) nutationCone.SetActive(showNutationCone && forceFree);
        if (spaceCone != null) spaceCone.SetActive(showSpaceCone && forceFree);
        if (bodyCone != null) bodyCone.SetActive(showBodyCone && forceFree);

        TrailRenderer trail = controller.ActiveRig.Trail;
        if (trail.enabled != showTrail) {
            trail.Clear();                                                      // no old segments when it comes back
            trail.enabled = showTrail;
        }
    }
}
