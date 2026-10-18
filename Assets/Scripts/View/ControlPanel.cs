using UnityEngine;
using UnityEngine.UI;                                               // Slider, Toggle, Button
using TMPro;                                                        // TextMeshProUGUI



public class ControlPanel : MonoBehaviour {
    [SerializeField] private GyroscopeController controller;
    [SerializeField] private Slider rpmSlider;
    [SerializeField] private TextMeshProUGUI rpmValue;

    void Start() {
        // range and initial value of the slider
        rpmSlider.minValue = 0f;
        rpmSlider.maxValue = 3000f;
        rpmSlider.value = controller.Rpm;

        // register the callback
        rpmSlider.onValueChanged.AddListener(OnRpmChanged);

        // show the initial value
        rpmValue.text = $"{rpmSlider.value:F0} rpm";
    }


    // pass the value to the controller, update the text (e.g. $"{value:F0} rpm")
    private void OnRpmChanged(float value) {
        controller.SetRpm(value);
        rpmValue.text = $"{value:F0} rpm";
    }
}
