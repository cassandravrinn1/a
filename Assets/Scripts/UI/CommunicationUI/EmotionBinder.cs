using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EmotionBinder : MonoBehaviour
{
    [Header("UI Bindings")]
    public Slider hopeSlider;
    public Slider happinessSlider;
    public Slider trustSlider;
    public Slider affinitySlider;
    public TextMeshProUGUI emotionValuesText;

    void Update()
    {
        if (PersonalitySystem.Instance == null) return;
        var emo = PersonalitySystem.Instance.Current;

        if (hopeSlider != null) hopeSlider.value = emo.Hope;
        if (happinessSlider != null) happinessSlider.value = emo.Happiness;
        if (trustSlider != null) trustSlider.value = emo.Trust;
        if (affinitySlider != null) affinitySlider.value = emo.Affinity;

        if (emotionValuesText != null)
        {
            emotionValuesText.text = $"Hope: {(emo.Hope * 100):F0}%\n" +
                                     $"Happiness: {(emo.Happiness * 100):F0}%\n" +
                                     $"Trust: {(emo.Trust * 100):F0}%\n" +
                                     $"Affinity: {(emo.Affinity * 100):F0}%";
        }
    }
}