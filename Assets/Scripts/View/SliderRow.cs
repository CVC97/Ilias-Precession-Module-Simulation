using System;              // Action<T>
using UnityEngine;
using UnityEngine.UI;
using TMPro;


public class SliderRow : MonoBehaviour {
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private Slider slider;
    [SerializeField] private TextMeshProUGUI valueText;

    private string unit;
    private string format;


    public void Setup(string labelText, float min, float max, float value, string unit, string format, Action<float> onChanged) {
        // store unit and format, set the label text
        this.unit = unit;
        this.format = format;
        label.text = labelText;

        // set range and value (BEFORE registering the listener — you know why)
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = value;

        // show the initial value
        valueText.text = Value2String(slider.value);

        // register one listener for updating text and calling onChanged(v)
        slider.onValueChanged.AddListener(v => {
            valueText.text = Value2String(v);
            onChanged(v);
        });
    }


    //  private helper for the value text
    private string Value2String(float value) {
        return value.ToString(format) + " " + unit;
    }
}
