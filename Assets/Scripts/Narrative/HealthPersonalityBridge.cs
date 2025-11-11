using UnityEngine;
using static HealthSystem;

/// <summary>
/// HealthSystem → PersonalitySystem 的桥接。
/// - 当苏拉米斯身体状态明显恶化或好转时，生成人格事件。
/// - 不修改 HealthSystem，不直接改健康，只影响情绪。
/// - 利用 PersonalityEvent 中的 Health/Fatigue/Stress 字段，让启发式生效。
/// 说明：这里使用你已有的 PersonalityEventTag，不新增枚举，避免动底层。
/// </summary>
public class HealthPersonalityBridge : MonoBehaviour
{
    [Header("触发阈值")]
    [Tooltip("疲劳变化超过该值时，视为一次显著变化")]
    public float fatigueDeltaThreshold = 0.15f;

    [Tooltip("受伤变化超过该值时，视为一次显著变化")]
    public float injuryDeltaThreshold = 0.15f;

    [Tooltip("生命力下降超过该值时，触发强烈负面事件")]
    public float vitalityDropThreshold = 0.15f;

    [Header("事件强度预设")]
    public float tiredImpact = 0.25f;
    public float severeImpact = 0.55f;
    public float criticalImpact = 0.9f;

    [Header("调试")]
    public bool logBridge = false;

    private HealthSystem _health;
    private bool _subscribed;

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void TrySubscribe()
    {
        if (_subscribed) return;

        _health = HealthSystem.Instance;
        if (_health == null) return;

        _health.OnSnapshotChanged += OnHealthChanged;
        _subscribed = true;

        if (logBridge)
            Debug.Log("[HealthPersonalityBridge] Subscribed to HealthSystem.OnSnapshotChanged.");
    }

    private void Unsubscribe()
    {
        if (_subscribed && _health != null)
        {
            _health.OnSnapshotChanged -= OnHealthChanged;
        }
        _subscribed = false;
    }

    private void Update()
    {
        // 防止运行时加载顺序问题，晚一点再尝试订阅
        if (!_subscribed && HealthSystem.Instance != null)
            TrySubscribe();
    }

    private void OnHealthChanged(HealthSnapshot oldSnap, HealthSnapshot newSnap)
    {
        var ps = PersonalitySystem.Instance;
        if (ps == null || !ps.IsAlive)
            return;

        // 计算归一化“整体健康”：这里只给 PersonalityEvent.e.Health 用
        float oldHealth = ComputeHealthScalar(oldSnap);
        float newHealth = ComputeHealthScalar(newSnap);
        float healthDelta = newHealth - oldHealth;

        // 1. 风险等级上升：说明更糟糕了
        if (newSnap.RiskLevel > oldSnap.RiskLevel)
        {
            switch (newSnap.RiskLevel)
            {
                case 1: // 轻微疲惫/不适
                    SendHealthEvent(
                        PersonalitySystem.PersonalityEventTag.Camp_Injury,   // 借用“受伤/不适”语义
                        tiredImpact,
                        newHealth,
                        newSnap.Fatigue,
                        EstimateStress(newSnap, isCritical: false));
                    break;

                case 2: // 严重
                    SendHealthEvent(
                        PersonalitySystem.PersonalityEventTag.Camp_Injury,
                        severeImpact,
                        newHealth,
                        newSnap.Fatigue,
                        EstimateStress(newSnap, isCritical: false));
                    break;

                case 3: // 濒危
                    SendHealthEvent(
                        PersonalitySystem.PersonalityEventTag.Camp_CriticalShortage, // 借用“极端危机”感
                        criticalImpact,
                        newHealth,
                        newSnap.Fatigue,
                        EstimateStress(newSnap, isCritical: true));
                    break;
            }
        }

        // 2. 健康显著下降（即使 RiskLevel 没变）
        if (-healthDelta > vitalityDropThreshold)
        {
            SendHealthEvent(
                PersonalitySystem.PersonalityEventTag.Camp_Injury,
                severeImpact,
                newHealth,
                newSnap.Fatigue,
                EstimateStress(newSnap, false));
        }

        // 3. 长期高疲劳：认为是“被过度使用/忽视需求”
        float fatigueDelta = newSnap.Fatigue - oldSnap.Fatigue;
        if (fatigueDelta > fatigueDeltaThreshold && newSnap.Fatigue > 0.7f)
        {
            SendHealthEvent(
                PersonalitySystem.PersonalityEventTag.Meta_IgnoreHerNeed,
                tiredImpact,
                newHealth,
                newSnap.Fatigue,
                EstimateStress(newSnap, false));
        }

        // 4. 健康改善：说明玩家/营地照顾了她，给予正向情绪
        if (newSnap.RiskLevel < oldSnap.RiskLevel || healthDelta > vitalityDropThreshold)
        {
            SendHealthEvent(
                PersonalitySystem.PersonalityEventTag.Meta_UseResourceForHer,
                tiredImpact,
                newHealth,
                newSnap.Fatigue,
                EstimateStress(newSnap, false));
        }
    }

    /// <summary>
    /// 将当前 HealthSnapshot 压成 0-1 的“整体健康值”供 PersonalityEvent 使用。
    /// 你可以根据自己最终 HealthSystem 的定义调整权重。
    /// </summary>
    private float ComputeHealthScalar(HealthSnapshot h)
    {
        // 示例：Vitality / Fatigue / Injury / Sickness 的简单组合
        float vitality = Mathf.Clamp01(h.Vitality);
        float fatigueCost = Mathf.Clamp01(h.Fatigue);
        float injuryCost = Mathf.Clamp01(h.Injury);
        float sickCost = Mathf.Clamp01(h.Sickness);

        float health = vitality;
        health -= 0.25f * fatigueCost;
        health -= 0.35f * injuryCost;
        health -= 0.3f * sickCost;

        return Mathf.Clamp01(health);
    }

    /// <summary>
    /// 基于健康状态粗略估算 Stress，0-1。
    /// 实际上只是让 PersonalityEvent 的 Stress 字段有个合理输入。
    /// </summary>
    private float EstimateStress(HealthSnapshot h, bool isCritical)
    {
        float stress = 0f;

        if (h.RiskLevel >= 2) stress += 0.4f;
        if (h.RiskLevel >= 3 || isCritical) stress += 0.4f;

        stress += Mathf.Clamp01(h.Fatigue) * 0.2f;

        return Mathf.Clamp01(stress);
    }

    private void SendHealthEvent(
        PersonalitySystem.PersonalityEventTag tag,
        float impact,
        float health01,
        float fatigue01,
        float stress01)
    {
        var ps = PersonalitySystem.Instance;
        if (ps == null) return;

        PersonalitySystem.PersonalityEvent e = new PersonalitySystem.PersonalityEvent
        {
            Tag = tag,
            Impact = Mathf.Clamp01(impact),

            // 这些场景一般不是“玩家说话”，所以 PlayerTone / TimeSinceLastContact 置 0
            PlayerTone = 0f,
            CampState = 0.5f,              // 可选：认为这是“局部事件”，不过多影响营地全局判断
            DayNormalized = 0f,
            TimeSinceLastContact = 0f,

            Health = Mathf.Clamp01(health01),
            Fatigue = Mathf.Clamp01(fatigue01),
            Stress = Mathf.Clamp01(stress01)
        };

        if (logBridge)
        {
            Debug.Log($"[HealthPersonalityBridge] Send {e.Tag}, impact={e.Impact}, H={e.Health:0.00}, F={e.Fatigue:0.00}, S={e.Stress:0.00}");
        }

        ps.RaiseEvent(e);
    }
}
