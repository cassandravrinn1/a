using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HealthBinder : MonoBehaviour
{
    [Header("UI Bindings")]
    public Slider vitalitySlider;     // 0~1
    public Slider fatigueSlider;      // 0~1 (Fatigue=SleepDebt)
    public Slider temperatureSlider;  // 映射：-1~1 -> 0~1
    public Slider injurySlider;       // 0~1
    public Slider sicknessSlider;     // 0~1

    [Header("Optional Text")]
    public TextMeshProUGUI healthValuesText;

    [Header("Temperature Mapping")]
    [Tooltip("Temperature=-1~1 映射到 Slider 0~1。勾选后：-1=>0, 0=>0.5, +1=>1")]
    public bool mapTemperatureTo01 = true;

    void Update()
    {
        if (HealthSystem.Instance == null) return;

        var s = HealthSystem.Instance.Snapshot;

        if (vitalitySlider != null) vitalitySlider.value = Mathf.Clamp01(s.Vitality);
        if (fatigueSlider != null) fatigueSlider.value = Mathf.Clamp01(s.Fatigue);
        if (injurySlider != null) injurySlider.value = Mathf.Clamp01(s.Injury);
        if (sicknessSlider != null) sicknessSlider.value = Mathf.Clamp01(s.Sickness);

        if (temperatureSlider != null)
        {
            float t = s.Temperature; // -1~1
            float t01 = mapTemperatureTo01 ? Mathf.InverseLerp(-1f, 1f, Mathf.Clamp(t, -1f, 1f)) : t;
            temperatureSlider.value = Mathf.Clamp01(t01);
        }

        if (healthValuesText != null)
        {
            float t = Mathf.Clamp(s.Temperature, -1f, 1f);
            float tC = t * 100f; // 这里仅做“相对体温偏差”展示，你也可以改成更直观的描述

            healthValuesText.text =
                $"Vitality: {(s.Vitality * 100f):F0}%\n" +
                $"Fatigue: {(s.Fatigue * 100f):F0}%\n" +
                $"Temperature: {t:+0.00;-0.00;0.00} (≈{tC:+0;-0;0}%)\n" +
                $"Injury: {(s.Injury * 100f):F0}%\n" +
                $"Sickness: {(s.Sickness * 100f):F0}%\n" +
                $"State: {s.HealthStateTag}  Risk: {s.RiskLevel}";
        }
    }
}
