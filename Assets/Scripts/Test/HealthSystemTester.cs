using TMPro;
using UnityEngine;
using static ProjectSulamith.Systems.ResourceSystem;

/// <summary>
/// HealthSystem + PersonalitySystem 联合调试面板（内部回调版）
/// - 订阅 HealthSystem.OnSnapshotChanged 显示当前身体状态快照；
/// - 每帧读取 PersonalitySystem.Current 显示当前情绪状态；
/// - 不依赖 EventBus，不假设不存在的字段名。
/// </summary>
public class HealthSystemTester : MonoBehaviour
{
    [Header("Health UI")]
    public TMP_Text vitalityText;
    public TMP_Text fatigueText;
    public TMP_Text temperatureText;
    public TMP_Text injuryText;
    public TMP_Text sicknessText;
    public TMP_Text riskLevelText;
    public TMP_Text stateTagText;

    [Header("Personality UI")]
    public TMP_Text hopeText;
    public TMP_Text happinessText;
    public TMP_Text trustText;
    public TMP_Text affinityText;

    private bool _subscribed;

    #region Unity Lifecycle

    private void OnEnable()
    {
        TrySubscribeHealth();
    }
  

    private void OnDisable()
    {
        UnsubscribeHealth();
    }

    private void Update()
    {
        // 处理运行时初始化顺序问题：HealthSystem/PersonalitySystem 晚一点出现也能挂上
        if (!_subscribed && HealthSystem.Instance != null)
            TrySubscribeHealth();

        UpdatePersonalityUI();
    }

    #endregion

    #region Subscribe HealthSystem

    private void TrySubscribeHealth()
    {
        if (_subscribed) return;
        if (HealthSystem.Instance == null) return;

        HealthSystem.Instance.OnSnapshotChanged += OnHealthChanged;
        _subscribed = true;

        Debug.Log("[HealthSystemTester] Subscribed to HealthSystem.OnSnapshotChanged.");

        // 初始化时强制刷新一次 UI
        var snap = HealthSystem.Instance.Snapshot;
        OnHealthChanged(snap, snap);
    }


    private void UnsubscribeHealth()
    {
        if (!_subscribed) return;
        if (HealthSystem.Instance != null)
            HealthSystem.Instance.OnSnapshotChanged -= OnHealthChanged;

        _subscribed = false;
    }

    #endregion

    #region Callbacks

    private void OnHealthChanged(HealthSystem.HealthSnapshot oldSnap, HealthSystem.HealthSnapshot newSnap)
    {
        // Vitality: 0-1
        vitalityText?.SetText($"Vitality  {newSnap.Vitality:0.00}");

        // Fatigue: 0-1
        fatigueText?.SetText($"Fatigue   {newSnap.Fatigue:0.00}");

        // Temperature: -1~1（这里直接显示归一化值，你以后想映射到℃再改）
        temperatureText?.SetText($"Temp偏差  {newSnap.Temperature:0.00}");

        injuryText?.SetText($"Injury    {newSnap.Injury:0.00}");
        sicknessText?.SetText($"Sickness  {newSnap.Sickness:0.00}");

        riskLevelText?.SetText($"Risk Lv.{newSnap.RiskLevel}");
        stateTagText?.SetText($"State [{newSnap.HealthStateTag}]");
    }

    #endregion

    #region Personality Polling

    private void UpdatePersonalityUI()
    {
        var ps = PersonalitySystem.Instance;
        if (ps == null || !ps.IsAlive)
            return;

        // 使用你已有的公开属性 Current（EmotionState）
        var emo = ps.Current;

        hopeText?.SetText($"Hope      {emo.Hope:0.00}");
        happinessText?.SetText($"Happy     {emo.Happiness:0.00}");
        trustText?.SetText($"Trust     {emo.Trust:0.00}");
        affinityText?.SetText($"Affinity  {emo.Affinity:0.00}");
    }

    #endregion
}
