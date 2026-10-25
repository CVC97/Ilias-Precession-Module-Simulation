using UnityEngine;



public class WebBridge : MonoBehaviour {
    [SerializeField] private GyroscopeController controller;
    [SerializeField] private VectorDisplay vectors;
    [SerializeField] private SimulationTime simTime;
    [SerializeField] private ParticleSystem snow;
    [SerializeField] private ParticleSystem snowFast;
    [SerializeField] private GameObject uiCanvas;                           // the uGUI canvas, hidden in the Web build
    [SerializeField] private bool hideUguiInBrowser = false;    // true once Angular provides the UI




    // called from JavaScript via SendMessage
    public void SetRpm(float v) {
        controller.SetRpm(v);
    }


    public void SetTheta0(float deg) {
        controller.SetTheta0Deg(deg);
    }


    public void SetPhiDot0(float v) {
        controller.SetPhiDot0(v);
    }


    public void SetTimeScale(float s) {
        simTime.SetTimeScale(s);
    }


    public void SetPaused(float on) {
        if (on > 0.5f) {
            simTime.SetPaused(true);
        } else {
            simTime.SetPaused(false);
        }
    }


    public void Restart() {
        controller.ResetSimulation();
    }


    public void SetShowL(float on) {
        vectors.ShowL = on > 0.5f;
    }


    public void SetShowOmega(float on) {
        vectors.ShowOmega = on > 0.5f;
    }


    public void SetShowTorque(float on) {
        vectors.ShowD = on > 0.5f;
    }


    public void SetSnow(float on) {
        snow.gameObject.SetActive(on > 0.5f);
        snowFast.gameObject.SetActive(on > 0.5f);
    }


    void Awake() {
        #if UNITY_WEBGL && !UNITY_EDITOR
            if (hideUguiInBrowser) uiCanvas.SetActive(false);        // in the browser the HTML UI takes over
        #endif
    }
}
