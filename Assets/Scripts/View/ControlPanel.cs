using UnityEngine;
using UnityEngine.UI;
using TMPro;



public class ControlPanel : MonoBehaviour {
    [SerializeField] private GyroscopeController controller;
    [SerializeField] private SimulationTime simTime;
    [SerializeField] private SliderRow rpmRow;
    [SerializeField] private SliderRow theta0Row;
    [SerializeField] private SliderRow phiDot0Row;
    [SerializeField] private SliderRow timeScaleRow;

    [SerializeField] private VectorDisplay vectors;
    [SerializeField] private Toggle toggleL;
    [SerializeField] private Toggle toggleOmega;
    [SerializeField] private Toggle toggleD;
    [SerializeField] private Toggle toggleSnow;
    [SerializeField] private ParticleSystem snow;
    [SerializeField] private ParticleSystem snowFast;

    [SerializeField] private Button resetButton;
    [SerializeField] private Button pauseButton;
    [SerializeField] private TextMeshProUGUI pauseLabel;

    [SerializeField] private TextMeshProUGUI readout;
    private float readoutTimer;


    void Start() {
        pauseButton.onClick.AddListener(simTime.TogglePause);
        resetButton.onClick.AddListener(controller.ResetSimulation);
        pauseLabel.text = simTime.Paused ? "Weiter" : "Pause";


        rpmRow.Setup("Drehzahl", 0f, 3000f, controller.Rpm, "rpm", "F0", controller.SetRpm);
        theta0Row.Setup("Neigung", 1, 89, controller.Theta0Deg, "°", "F0", controller.SetTheta0Deg);
        phiDot0Row.Setup("Präzession", -10, 10, controller.PhiDot0, "rad/s", "F2", controller.SetPhiDot0);
        timeScaleRow.Setup("Zeitlupe", -2f, 0f, Mathf.Log10(simTime.TimeScale), "×", "G2",
            v => simTime.SetTimeScale(Mathf.Pow(10f, v)),
            v => Mathf.Pow(10f, v));

        toggleL.isOn = vectors.ShowL;
        toggleOmega.isOn = vectors.ShowOmega;
        toggleD.isOn = vectors.ShowD;

        toggleL.onValueChanged.AddListener(v => vectors.ShowL = v);
        toggleOmega.onValueChanged.AddListener(v => vectors.ShowOmega = v);
        toggleD.onValueChanged.AddListener(v => vectors.ShowD = v);

        // the module scene has no snow (and no snow toggle)
        if (snow != null) {
            toggleSnow.isOn = snow.gameObject.activeSelf;
            toggleSnow.onValueChanged.AddListener(
                v => {
                    snow.gameObject.SetActive(v);
                    snowFast.gameObject.SetActive(v);
                }
            );
        }
    }


    void Update() {
        pauseLabel.text = simTime.Paused ? "Weiter" : "Pause";

        readoutTimer += Time.unscaledDeltaTime;
        // only ~10× per second (otherwise flickering unreadable)
        if (readoutTimer < 0.1f) {
            return;
        }                                    
        readoutTimer = 0f;

        GyroscopeModel model = controller.Model;
        if (model == null) return;

        readout.text =
            $"θ = {model.Theta * Mathf.Rad2Deg:F1}°\n" +
            $"dφ/dt= {model.PhiDot:F2} rad/s\n" +
            $"ΔE/(mgl) = {controller.EnergyError:E1}" +
            (controller.SpinDisplayLimited ? "\nRotor: Darstellung verlangsamt" : "");
    }
}
