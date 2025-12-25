/*
 * Project: ProjectSulamith
 * File: HealthSystem.cs
 * Author: [Cassandra]
 * Description:
 *   苏拉米斯身体健康系统（唯一真源）。
 *   - 通过订阅 TimeManager 发出的 GameTickEvent 按“游戏分钟”推进。
 *   - 支持实时通讯模式 + 加速模拟模式（由 TimeManager 统一驱动）。
 *   - 输出 HealthSnapshot 给 UI / Ink / Personality 等系统只读使用。
 *   - 不反向修改人格，仅通过 HealthPersonalityBridge 映射为情绪事件。
 */

using System;
using UnityEngine;
using ProjectSulamith.Core; // 用于 TimeManager / EventBus / GameTickEvent

/// <summary>
/// 健康系统。
/// </summary>
public class HealthSystem : MonoBehaviour
{
    #region Singleton

    public static HealthSystem Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[HealthSystem] Duplicate instance, destroying this one.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        ResetInternalState();
        RebuildSnapshot(force: true);
    }

    #endregion

    #region Types & Snapshot

    /// <summary>
    /// 当前活动强度，用于体力/脱水/应激消耗。
    /// 外部系统（排班/剧情）应根据场景调用 SetActivity。
    /// </summary>
    public enum ActivityLevel
    {
        Resting,    // 躺着/安静
        Idle,       // 坐着、轻度工作
        LightWork,  // 巡检、走动
        HardWork,   // 抢修、奔跑
        Panic       // 极端应激
    }

    public enum HealthEventType
    {
        Damage,
        Heal,

        AddFatigue,
        RecoverFatigue,

        ColdExposure,
        HeatExposure,

        Injury,
        TreatInjury,

        Infection,
        CureInfection,

        DrinkWater,
        TakeMedicine,

        FullRest
    }

    [Serializable]
    public struct HealthEvent
    {
        public HealthEventType Type;
        public float Amount;          // >0，具体含义看 Type
        public float DurationHours;   // 暂留接口，当前未用
        public string SourceTag;      // 调试可见
    }

    /// <summary>
    /// 对外公开的健康快照。
    /// 注意：本结构被 HealthPersonalityBridge 直接使用，请谨慎改名。
    /// </summary>
    [Serializable]
    public struct HealthSnapshot
    {
        public float Vitality;        // 0-1：生命力（长期）
        public float Fatigue;         // 0-1：疲劳/睡眠债务（高=困）
        public float Temperature;     // -1~1：核心体温偏差（负=冷，正=热）
        public float Injury;          // 0-1
        public float Sickness;        // 0-1

        public string HealthStateTag; // "ok" / "tired" / "severe" / "critical"
        public int RiskLevel;         // 0-3
    }

    /// <summary>
    /// 健康快照变化事件。
    /// HealthPersonalityBridge / UI 直接订阅这个。
    /// </summary>
    public event Action<HealthSnapshot, HealthSnapshot> OnSnapshotChanged;

    public HealthSnapshot Snapshot { get; private set; }

    #endregion

    #region Inspector Config

    [Header("初始状态")]
    [Range(0f, 1f)] public float startVitality = 1f;
    [Range(0f, 1f)] public float startStamina = 0.8f;
    [Range(0f, 1f)] public float startHydration = 1f;

    [Header("睡眠 / 疲劳（每小时）")]
    [Tooltip("清醒时睡眠债务累积速度")]
    public float sleepDebtPerHourAwake = 0.08f;
    [Tooltip("睡眠时睡眠债务恢复速度")]
    public float sleepDebtRecoverPerHour = 0.25f;

    [Header("脱水（每小时）")]
    [Tooltip("基础脱水速度")]
    public float baseHydrationLossPerHour = 0.03f;

    [Header("体力消耗 / 恢复（每小时，受活动影响）")]
    public float staminaRecoverRestPerHour = 0.5f;
    public float staminaCostIdlePerHour = 0.02f;
    public float staminaCostLightPerHour = 0.08f;
    public float staminaCostHardPerHour = 0.25f;
    public float staminaCostPanicPerHour = 0.5f;

    [Header("体温（每小时）")]
    [Tooltip("极冷环境下朝 -1 漂移速率")]
    public float tempLossColdPerHour = 0.15f;
    [Tooltip("极热环境下朝 +1 漂移速率")]
    public float tempGainHotPerHour = 0.15f;
    [Tooltip("舒适环境下体温向 0 收敛速率")]
    public float tempRecoverNeutralPerHour = 0.2f;

    [Header("严重状态对 Vitality 的伤害（每小时）")]
    public float vitalityLossSevereHypoPerHour = 0.2f;
    public float vitalityLossSevereDehydratePerHour = 0.15f;
    public float vitalityLossCriticalIllnessPerHour = 0.15f;
    [Tooltip("生命力低于此值视为濒危")]
    public float criticalVitalityThreshold = 0.15f;

    [Header("外部环境（由其他系统写入）")]
    [Tooltip("环境温度：0=极冷，0.5=舒适，1=极热")]
    [Range(0f, 1f)] public float EnvTemperature01 = 0.3f;
    [Tooltip("防护强度：0=无遮蔽，1=完美防护")]
    [Range(0f, 1f)] public float Protection01 = 0.5f;

    [Header("调试")]
    public bool logEvents = false;
    public bool logTick = false;

    #endregion

    #region Runtime State

    // 内部连续状态
    float _vitality;
    float _stamina;
    float _sleepDebt;     // 0-1
    float _hydration;     // 0-1
    float _temperature;   // -1~1
    float _injury;        // 0-1
    float _sickness;      // 0-1

    bool _isSleeping;
    ActivityLevel _activity = ActivityLevel.Idle;

    #endregion

    #region Unity Lifecycle & EventBus

    private void OnEnable()
    {
        // 订阅 GameTickEvent（由 TimeManager 广播，单位：游戏分钟）
        EventBus.Instance?.Subscribe<GameTickEvent>(OnGameTick);
    }

    private void OnDisable()
    {
        EventBus.Instance?.Unsubscribe<GameTickEvent>(OnGameTick);
    }

    private void OnGameTick(GameTickEvent e)
    {
        // TimeManager 已经把真实时间 * 模式倍率 转成了 DeltaMinutes
        // Realtime 模式：1:1；Simulation 模式：加速；Paused：不发 Tick。
        TickByGameMinutes(e.DeltaMinutes);
        //Debug.Log($"[HealthSystem] Received GameTickEvent Δm={e.DeltaMinutes}");

    }

    #endregion

    #region Public API

    public void SetActivity(ActivityLevel level)
    {
        _activity = level;
    }

    public void SetSleeping(bool sleeping)
    {
        _isSleeping = sleeping;
        if (sleeping)
            _activity = ActivityLevel.Resting;
    }

    /// <summary>
    /// 外部系统（剧情/模拟）对她的身体状态施加一次离散事件。
    /// 所有直接操作健康状态的逻辑请尽量集中走这里。
    /// </summary>
    public void ApplyEvent(HealthEvent e)
    {
        switch (e.Type)
        {
            case HealthEventType.Damage:
                _vitality -= e.Amount;
                break;

            case HealthEventType.Heal:
                _vitality += e.Amount;
                break;

            case HealthEventType.AddFatigue:
                _sleepDebt += e.Amount;
                break;

            case HealthEventType.RecoverFatigue:
                _sleepDebt -= e.Amount;
                break;

            case HealthEventType.ColdExposure:
                _temperature -= e.Amount;
                break;

            case HealthEventType.HeatExposure:
                _temperature += e.Amount;
                break;

            case HealthEventType.Injury:
                _injury += e.Amount;
                break;

            case HealthEventType.TreatInjury:
                _injury -= e.Amount;
                break;

            case HealthEventType.Infection:
                _sickness += e.Amount;
                break;

            case HealthEventType.CureInfection:
                _sickness -= e.Amount;
                break;

            case HealthEventType.DrinkWater:
                _hydration += e.Amount;
                break;

            case HealthEventType.TakeMedicine:
                _sickness -= 0.2f * e.Amount;
                _stamina += 0.1f * e.Amount;
                break;

            case HealthEventType.FullRest:
                _sleepDebt *= 0.3f;
                _stamina = Mathf.Max(_stamina, 0.6f);
                break;
        }

        if (logEvents)
        {
            Debug.Log($"[HealthSystem] ApplyEvent {e.Type}, amount={e.Amount:0.00}, src={e.SourceTag}");
        }

        ClampInternal();
        RebuildSnapshot(force: true);
    }

    #endregion

    #region Tick Logic

    /// <summary>
    /// 用游戏分钟推进健康状态。
    /// Realtime 模式下 = 真实分钟；
    /// Simulation 模式下 = 加速后的分钟（她身体也会随时间快进）。
    /// </summary>
    private void TickByGameMinutes(float deltaMinutes)
    {
        if (deltaMinutes <= 0f) return;

        float dh = deltaMinutes / 60f; // 换算成“游戏小时”

        UpdateSleep(dh);
        UpdateHydration(dh);
        UpdateStamina(dh);
        UpdateTemperature(dh);
        UpdateIllnessAndInjury(dh);
        ApplyCriticalEffects(dh);

        ClampInternal();
        RebuildSnapshot(force: true);

        if (logTick)
        {
            Debug.Log(
                $"[HealthSystem] Tick Δm={deltaMinutes:F2} | V={_vitality:F2} Fatigue={_sleepDebt:F2} " +
                $"H2O={_hydration:F2} T={_temperature:F2} Inj={_injury:F2} Sick={_sickness:F2}");
        }
    }

    private void UpdateSleep(float dh)
    {
        if (_isSleeping)
            _sleepDebt -= sleepDebtRecoverPerHour * dh;
        else
            _sleepDebt += sleepDebtPerHourAwake * dh;
    }

    private void UpdateHydration(float dh)
    {
        float actMul = _activity switch
        {
            ActivityLevel.Resting => 0.5f,
            ActivityLevel.Idle => 1.0f,
            ActivityLevel.LightWork => 1.3f,
            ActivityLevel.HardWork => 1.8f,
            ActivityLevel.Panic => 2.2f,
            _ => 1f
        };

        _hydration -= baseHydrationLossPerHour * actMul * dh;
    }

    private void UpdateStamina(float dh)
    {
        if (_isSleeping || _activity == ActivityLevel.Resting)
        {
            _stamina += staminaRecoverRestPerHour * (1f - _sleepDebt) * dh;
        }
        else
        {
            float cost = _activity switch
            {
                ActivityLevel.Idle => staminaCostIdlePerHour,
                ActivityLevel.LightWork => staminaCostLightPerHour,
                ActivityLevel.HardWork => staminaCostHardPerHour,
                ActivityLevel.Panic => staminaCostPanicPerHour,
                _ => 0f
            };
            _stamina -= cost * dh;
        }

        // 缺觉降低体力上限
        float maxStamina = Mathf.Lerp(0.4f, 1f, 1f - _sleepDebt);
        if (_stamina > maxStamina) _stamina = maxStamina;
    }

    private void UpdateTemperature(float dh)
    {
        float coldFactor = Mathf.Clamp01((0.5f - EnvTemperature01) * 2f * (1f - Protection01));
        float hotFactor = Mathf.Clamp01((EnvTemperature01 - 0.5f) * 2f * (1f - Protection01));

        if (coldFactor > 0f)
        {
            _temperature -= tempLossColdPerHour * coldFactor * dh;
        }
        else if (hotFactor > 0f)
        {
            _temperature += tempGainHotPerHour * hotFactor * dh;
        }
        else
        {
            // 舒适环境下体温向 0 收敛
            if (Mathf.Abs(_temperature) > 0.001f)
            {
                float sign = Mathf.Sign(_temperature);
                _temperature -= sign * tempRecoverNeutralPerHour * dh;
            }
        }
    }

    private void UpdateIllnessAndInjury(float dh)
    {
        if (_injury > 0f)
        {
            float factor = Mathf.Clamp01(0.6f - _injury);
            _injury -= 0.03f * factor * dh;
        }

        if (_sickness > 0f)
        {
            float factor = Mathf.Clamp01(0.5f - _sickness);
            _sickness -= 0.02f * factor * dh;
        }
    }

    private void ApplyCriticalEffects(float dh)
    {
        // 严重低体温
        if (_temperature <= -0.7f)
        {
            float t = Mathf.InverseLerp(-0.7f, -1f, Mathf.Clamp(_temperature, -1f, -0.7f));
            _vitality -= vitalityLossSevereHypoPerHour * t * dh;
        }

        // 严重脱水
        if (_hydration <= 0.15f)
        {
            float t = Mathf.InverseLerp(0.15f, 0f, Mathf.Clamp(_hydration, 0f, 0.15f));
            _vitality -= vitalityLossSevereDehydratePerHour * t * dh;
        }

        // 严重疾病
        if (_sickness >= 0.8f)
        {
            _vitality -= vitalityLossCriticalIllnessPerHour * dh;
        }

        // 极端缺觉 + 过劳
        if (_sleepDebt > 0.9f && _stamina < 0.1f)
        {
            _vitality -= 0.05f * dh;
        }

        // 死亡 → 锁人格
        if (_vitality <= 0f)
        {
            _vitality = 0f;
            if (PersonalitySystem.Instance != null)
                PersonalitySystem.Instance.SetAlive(false);
        }
    }

    #endregion

    #region Snapshot & Helpers

    private void ResetInternalState()
    {
        _vitality = Mathf.Clamp01(startVitality);
        _stamina = Mathf.Clamp01(startStamina);
        _hydration = Mathf.Clamp01(startHydration);
        _sleepDebt = 0f;
        _temperature = 0f;
        _injury = 0f;
        _sickness = 0f;
        _isSleeping = false;
        _activity = ActivityLevel.Idle;
    }

    private void ClampInternal()
    {
        _vitality = Mathf.Clamp01(_vitality);
        _stamina = Mathf.Clamp01(_stamina);
        _sleepDebt = Mathf.Clamp01(_sleepDebt);
        _hydration = Mathf.Clamp01(_hydration);
        _temperature = Mathf.Clamp(_temperature, -1f, 1f);
        _injury = Mathf.Clamp01(_injury);
        _sickness = Mathf.Clamp01(_sickness);
    }

    private void RebuildSnapshot(bool force)
    {
        var old = Snapshot;

        float fatiguePublic = _sleepDebt;
        float tempPublic = _temperature;
        float injuryPublic = _injury;
        float sickPublic = _sickness;

        string tag = "ok";
        int risk = 0;

        bool shivering = tempPublic < -0.3f;
        bool weak = _stamina < 0.2f || _vitality < 0.4f;
        bool badSick = sickPublic > 0.6f;
        bool badInjury = injuryPublic > 0.6f;
        bool veryTired = fatiguePublic > 0.9f;

        if (_vitality <= criticalVitalityThreshold ||
            tempPublic <= -0.9f ||
            _hydration <= 0.05f)
        {
            tag = "critical";
            risk = 3;
        }
        else if (badInjury || badSick || veryTired)
        {
            tag = "severe";
            risk = 2;
        }
        else if (shivering || weak || fatiguePublic > 0.6f)
        {
            tag = "tired";
            risk = 1;
        }

        Snapshot = new HealthSnapshot
        {
            Vitality = _vitality,
            Fatigue = fatiguePublic,
            Temperature = tempPublic,
            Injury = injuryPublic,
            Sickness = sickPublic,
            HealthStateTag = tag,
            RiskLevel = risk
        };

        if (force || HasSignificantChange(old, Snapshot))
        {
            OnSnapshotChanged?.Invoke(old, Snapshot);
        }
    }

    private bool HasSignificantChange(HealthSnapshot a, HealthSnapshot b)
    {
        if (a.HealthStateTag != b.HealthStateTag) return true;
        if (a.RiskLevel != b.RiskLevel) return true;

        if (Mathf.Abs(a.Vitality - b.Vitality) > 0.05f) return true;
        if (Mathf.Abs(a.Fatigue - b.Fatigue) > 0.08f) return true;
        if (Mathf.Abs(a.Temperature - b.Temperature) > 0.10f) return true;
        if (Mathf.Abs(a.Injury - b.Injury) > 0.08f) return true;
        if (Mathf.Abs(a.Sickness - b.Sickness) > 0.08f) return true;

        return false;
    }

    #endregion
}
