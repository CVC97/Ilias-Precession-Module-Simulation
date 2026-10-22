using UnityEngine;
using UnityEngine.UI;
using TMPro;



public class ControlPanel : MonoBehaviour {
    [SerializeField] private GyroscopeController controller;
    [SerializeField] private SliderRow rpmRow;
    [SerializeField] private SliderRow theta0Row;
    [SerializeField] private SliderRow phiDot0Row;
    [SerializeField] private SliderRow timeScaleRow;

    [SerializeField] private VectorDisplay vectors;
    [SerializeField] private Toggle toggleL;
    [SerializeField] private Toggle toggleOmega;
    [SerializeField] private Toggle toggleD;
    [SerializeField] private ParticleSystem snow;
    [SerializeField] private Toggle toggleSnow;

    [SerializeField] private Button resetButton;
    [SerializeField] private Button pauseButton;
    [SerializeField] private TextMeshProUGUI pauseLabel;
    private bool paused;

    [SerializeField] private TextMeshProUGUI readout;
    private float readoutTimer;

    



    void Start() {
        pauseButton.onClick.AddListener(TogglePause);
        resetButton.onClick.AddListener(controller.ResetSimulation);

        rpmRow.Setup("Drehzahl", 0f, 3000f, controller.Rpm, "rpm", "F0", controller.SetRpm);
        theta0Row.Setup("Neigung", 1, 89, controller.Theta0Deg, "°", "F0", controller.SetTheta0Deg);
        phiDot0Row.Setup("Präzession", -10, 10, controller.PhiDot0, "rad/s", "F2", controller.SetPhiDot0);
        timeScaleRow.Setup("Zeitlupe", 0.05f, 1f, Time.timeScale, "×", "F2", v => {
            if (!paused) Time.timeScale = v;
        });

        toggleL.isOn = vectors.ShowL;
        toggleOmega.isOn = vectors.ShowOmega;
        toggleD.isOn = vectors.ShowD;
        toggleSnow.isOn = snow.gameObject.activeSelf;

        toggleL.onValueChanged.AddListener(v => vectors.ShowL = v);
        toggleOmega.onValueChanged.AddListener(v => vectors.ShowOmega = v);
        toggleD.onValueChanged.AddListener(v => vectors.ShowD = v);
        toggleSnow.onValueChanged.AddListener(v => snow.gameObject.SetActive(v));
    }


    void TogglePause() {
        paused = !paused;
        pauseLabel.text = paused ? "Weiter" : "Pause";
        Time.timeScale = paused ? 0 : timeScaleRow.Value;
    }


    void Update() {
        readoutTimer += Time.unscaledDeltaTime;
        if (readoutTimer < 0.1f) return;                                    // only ~10× per second (otherwise flickering unreadable)
        readoutTimer = 0f;

        GyroscopeModel model = controller.Model;
        if (model == null) return;

        readout.text =
            $"θ = {model.Theta * Mathf.Rad2Deg:F1}°\n" +
            $"dφ/dt= {model.PhiDot:F2} rad/s\n" +
            $"ΔE/(mgl) = {controller.EnergyError:E1}";
    }
}
