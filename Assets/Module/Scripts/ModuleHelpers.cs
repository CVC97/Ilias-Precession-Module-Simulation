using UnityEngine;


// Switchboard for everything that helps reading the motion but isn't part of the apparatus: vectors, their labels,
// axes, the precession arrow, the trail. The page sets these through its presets (step 5); in the editor the
// Inspector toggles work live, because the flags are applied every frame.
public class ModuleHelpers : MonoBehaviour {
    [SerializeField] private ModuleGyroscopeController controller;
    [SerializeField] private VectorDisplay vectors;
    [SerializeField] private GameObject labels;
    [SerializeField] private GameObject figureAxis;
    [SerializeField] private GameObject verticalAxis;
    [SerializeField] private GameObject precessionCone;                         // cone swept by the figure axis, with its ω_P label
    [SerializeField] private GameObject precessionArc;                          // ω_P as a curved arrow (kept for comparison)
    [SerializeField] private GameObject precessionVector;                       // ω⃗_P (kept for comparison)

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
    public void SetShowTrail(bool on) => showTrail = on;


    void LateUpdate() {
        vectors.ShowL = showL;
        vectors.ShowOmega = showOmega;
        vectors.ShowD = showD;
        labels.SetActive(showLabels);
        figureAxis.SetActive(showFigureAxis);
        verticalAxis.SetActive(showVerticalAxis);
        if (precessionCone != null) precessionCone.SetActive(showPrecessionCone);
        precessionArc.SetActive(showPrecessionArc);
        if (precessionVector != null) precessionVector.SetActive(showPrecessionVector);

        TrailRenderer trail = controller.ActiveRig.Trail;
        if (trail.enabled != showTrail) {
            trail.Clear();                                                      // no old segments when it comes back
            trail.enabled = showTrail;
        }
    }
}
