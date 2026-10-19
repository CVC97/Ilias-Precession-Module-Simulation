using UnityEngine;
using UnityEngine.UI;                                               // Slider, Toggle, Button
using TMPro;                                                        // TextMeshProUGUI



public class ControlPanel : MonoBehaviour {
    [SerializeField] private GyroscopeController controller;
    [SerializeField] private SliderRow rpmRow;
    [SerializeField] private SliderRow theta0Row;
    [SerializeField] private SliderRow phiDot0Row;
    [SerializeField] private SliderRow timeScaleRow;


    void Start() {
        rpmRow.Setup("Drehzahl", 0f, 3000f, controller.Rpm, "rpm", "F0", controller.SetRpm);
        theta0Row.Setup("Neigung", 1, 89, controller.Theta0Deg, "deg", "F0", controller.SetTheta0Deg);
        phiDot0Row.Setup("Präzession", -10, 10, controller.PhiDot0, "°", "F2", controller.SetPhiDot0);
        timeScaleRow.Setup("Zeitlupe", 0.05f, 1f, Time.timeScale, "×", "F2", v => Time.timeScale = v);
    }
}
