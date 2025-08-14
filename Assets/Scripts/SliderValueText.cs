using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SliderValueText : MonoBehaviour
{
    [SerializeField] private Slider slider;          // auto-filled in Reset()
    [SerializeField] private List<TMP_Text> outputs; // assign both labels here
    [SerializeField] private string format = "{0:0}"; // e.g. "{0:0}", "{0:0.0}"
    [SerializeField] private float multiplier = 1f;   // set to 100 if slider is 0..1 but you want 0..100
    [SerializeField] private string suffix = "";      // e.g. "%"

    private void Reset() => slider = GetComponent<Slider>();

    private void OnEnable()
    {
        if (!slider) slider = GetComponent<Slider>();
        slider.onValueChanged.AddListener(OnValueChanged);
        OnValueChanged(slider.value); // initialize labels
    }

    private void OnDisable()
    {
        if (slider) slider.onValueChanged.RemoveListener(OnValueChanged);
    }

    private void OnValueChanged(float v)
    {
        string text = string.Format(format, v * multiplier) + suffix;
        for (int i = 0; i < outputs.Count; i++)
            if (outputs[i]) outputs[i].text = text;
    }
}