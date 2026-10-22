using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;


public class SliderRow : MonoBehaviour {
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private Slider slider;
    [SerializeField] private TextMeshProUGUI valueText;

    private string unit;
    private string format;
    private Func<float, float> toDisplay;
    public float Value => slider.value;


    public void Setup(string labelText, float min, float max, float value, string unit, string format, 
        Action<float> onChanged, Func<float, float> toDisplay = null) {
        // store unit and format, set the label text
        this.unit = unit;
        this.format = format;
        this.toDisplay = toDisplay;
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
        float shown = toDisplay != null ? toDisplay(value) : value;
        return shown.ToString(format) + " " + unit;
    }
}
