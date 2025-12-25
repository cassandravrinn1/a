using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 苏拉米斯人格与情绪系统核心入口。
/// - 输入：结构化的 PersonalityEvent（由对话、营地事件、天气等触发）
/// - 内部：Hope / Happiness / Trust / Affinity + Guilt + 记忆偏置
/// - 输出：平滑后的情绪状态（供 Ink、UI、行为逻辑查询）
///
/// 设计要点：
/// 1. Tag + 语义轴（Valence / Agency / Moral / Social / Control / Novelty / ContextFocus）驱动事件评价。
/// 2. Guilt 独立维度，只作为调制项，不直接输出为玩家可见情绪。
/// 3. 短期记忆（episodic） + 长期记忆（per Tag 聚合）沉积偏置，塑造“一贯印象”。（摘要可作为 NN 输入）
/// 4. 所有输出限制在 [0,1]，参数可在 Inspector 中直接调参。
/// 5. EvaluateNetworkLikeMapping 可被真正的前馈网络替换（本文件已提供 NN 接口）。
/// </summary>
public class PersonalitySystem : MonoBehaviour
{
    #region Singleton

    public static PersonalitySystem Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[PersonalitySystem] Duplicate instance, destroying this one.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    #endregion

    #region Types

    public enum PersonalityEventTag
    {
        // 玩家态度
        Player_Comfort,
        Player_Encourage,
        Player_Apologize,
        Player_Harsh,
        Player_Ignorant,
        Player_Objectify,

        // 诚实 & 承诺
        Player_KeepPromise,
        Player_BreakPromise,
        Player_LieDetected,
        Player_Transparency,

        // 营地资源 & 安全
        Camp_ResourceUp,
        Camp_ResourceDown,
        Camp_CriticalShortage,
        Camp_SecurityImproved,
        Camp_SecurityBreach,

        // 人员
        Camp_Casualty,
        Camp_Injury,
        Camp_SuccessfulRescue,
        Camp_LossDueToDecision_Player,
        Camp_LossAvoidedByPlayer,

        // 天气 / 阶段
        Weather_StormStart,
        Weather_StormPeak,
        Weather_StormEnd,
        Milestone_EarlyGame,
        Milestone_MidGame,
        Milestone_FinalStorm,

        // 元事件：关系 / 陪伴模式
        Meta_LongNoContact,
        Meta_FrequentCheckIn,
        Meta_UseResourceForHer,
        Meta_IgnoreHerNeed,
        Meta_TalkOnlyWhenNeed,
    }

    /// <summary>
    /// 外部系统发送的一次事件。
    /// Tag 表示类型；其余为上下文参数。
    /// 七个语义轴由系统内部根据 Tag + Context 计算。
    /// </summary>
    [Serializable]
    public struct PersonalityEvent
    {
        public PersonalityEventTag Tag;

        [Range(0f, 1f)]
        public float Impact;                // 事件规模 / 冲击强度（0 用作“未特别标注”，内部会给兜底）

        [Range(-1f, 1f)]
        public float PlayerTone;           // 玩家语气（适用于对话类）

        [Range(0f, 1f)]
        public float CampState;            // 营地总体状态

        [Range(0f, 1f)]
        public float DayNormalized;        // 当前进度（0~1）

        [Range(0f, 1f)]
        public float TimeSinceLastContact; // 冷淡时长（归一化）

        // 健康状态参数（外部系统同步进来）
        [Range(0f, 1f)]
        public float Health;   // 1 = 状态很好, 0 = 非常糟糕/重伤/死亡

        [Range(0f, 1f)]
        public float Fatigue;  // 0 = 不累, 1 = 极度疲劳

        [Range(0f, 1f)]
        public float Stress;   // 0 = 放松, 1 = 高压/惊恐

        // 七个语义轴（内部计算填充）
        [HideInInspector] public float Valence;      // -1 ~ 1
        [HideInInspector] public float Agency;       // -1 ~ 1
        [HideInInspector] public float Moral;        // -1 ~ 1
        [HideInInspector] public float Social;       // -1 ~ 1
        [HideInInspector] public float Control;      //  0 ~ 1
        [HideInInspector] public float Novelty;      //  0 ~ 1
        [HideInInspector] public float ContextFocus; //  0 ~ 1（0 环境 / 1 人际）
    }

    [Serializable]
    public struct EmotionState
    {
        [Range(0f, 1f)] public float Hope;
        [Range(0f, 1f)] public float Happiness;
        [Range(0f, 1f)] public float Trust;
        [Range(0f, 1f)] public float Affinity;
    }

    /// <summary>
    /// 单条“记忆事件”：即时情绪影响，用于短期残留。
    /// </summary>
    [Serializable]
    private struct MemoryEntry
    {
        public PersonalityEventTag Tag;
        public Vector4 Delta; // 对情绪的即时影响（事件响应层输出）
        public float Time;    // Time.time
    }

    /// <summary>
    /// 长期记忆统计：按 Tag 聚合。
    /// </summary>
    private sealed class LongTermStat
    {
        public int Count;
        public Vector4 Sum; // 所有 Delta 累加，用于求平均
    }

    #endregion

    #region Inspector Config

    [Header("Initial State")]
    [SerializeField]
    private EmotionState initialEmotion = new EmotionState
    {
        Hope = 0.6f,
        Happiness = 0.6f,
        Trust = 0.5f,
        Affinity = 0.5f
    };

    [Header("Dynamics")]
    [Tooltip("情绪惯性（越高越钝感：0 = 立即贴合，0.9 = 非常缓慢）")]
    [Range(0f, 0.99f)]
    public float emotionInertia = 0.7f;

    [Tooltip("随机扰动幅度，让她不要完全机械")]
    [Range(0f, 0.1f)]
    public float noiseAmplitude = 0.02f;

    [Header("Guilt Settings")]
    [Tooltip("当前内疚感 0~1，仅作为输入使用")]
    [Range(0f, 1f)]
    public float guilt = 0f;

    [Tooltip("内疚自然衰减速度（每现实分钟减少量）")]
    [Range(0f, 0.5f)]
    public float guiltDecayPerMinute = 0.05f;

    [Header("Memory Settings")]
    [Tooltip("短期记忆保留的最大事件条数")]
    public int episodicCapacity = 32;

    [Tooltip("短期记忆窗口（秒）：越大，最近事件影响持续越久")]
    public float shortTermWindow = 300f;

    [Tooltip("短期记忆对情绪更新的整体权重（只用于启发式回退时）")]
    [Range(0f, 2f)]
    public float shortTermWeight = 0.6f;

    [Tooltip("长期记忆偏置整体权重（只用于启发式回退时）")]
    [Range(0f, 2f)]
    public float longTermWeight = 0.4f;

    [Header("Neural Network (Optional)")]
    [Tooltip("如赋值，将使用该配置驱动前馈网络；为空则使用内建启发式。")]
    public PersonalityNNConfig nnConfig;

    [Header("Debug")]
    public bool logEvents = false;
    public bool logSemanticAxes = false;
    public bool logMemory = false;

    #endregion

    #region Runtime State

    private EmotionState _current;
    private bool _isAlive = true;

    private PersonalityNN _nn;

    public EmotionState Current => _current;
    public bool IsAlive => _isAlive;

    public Vector4 CurrentVector =>
        new Vector4(_current.Hope, _current.Happiness, _current.Trust, _current.Affinity);

    private readonly List<MemoryEntry> _episodicMemories = new List<MemoryEntry>();
    private readonly Dictionary<PersonalityEventTag, LongTermStat> _longTermStats =
        new Dictionary<PersonalityEventTag, LongTermStat>();

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        _current = initialEmotion;

        if (nnConfig != null && nnConfig.IsValid())
        {
            _nn = new PersonalityNN(nnConfig);
            Debug.Log("[PersonalitySystem] Using NN config for emotion mapping.");
        }
        else
        {
            if (nnConfig != null)
                Debug.LogWarning("[PersonalitySystem] NN config invalid, fallback to heuristic mapping.");
        }
    }

    private void Update()
    {
        if (!_isAlive) return;

        // Guilt 随时间衰减
        if (guilt > 0f && guiltDecayPerMinute > 0f)
        {
            float delta = guiltDecayPerMinute / 60f * Time.deltaTime;
            guilt = Mathf.Max(0f, guilt - delta);
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// 外部调用入口：推入一次事件。
    /// </summary>
    public void RaiseEvent(PersonalityEvent e)
    {
        if (!_isAlive)
            return;

        // Health = 0 视为苏拉米斯死亡：锁定人格系统
        if (e.Health <= 0f)
        {
            if (_isAlive && logEvents)
                Debug.Log("[PersonalitySystem] Received event with Health <= 0. Personality updates halted.");
            _isAlive = false;
            return;
        }

        if (logEvents)
            Debug.Log($"[PersonalitySystem] Event: {e.Tag}, impact={e.Impact:0.00}, tone={e.PlayerTone:0.00}");

        // 1. 填充七个语义轴
        ComputeSemanticAxes(ref e);

        if (logSemanticAxes)
        {
            Debug.Log(
                $"[Axes] {e.Tag} | V={e.Valence:F2} A={e.Agency:F2} M={e.Moral:F2} " +
                $"S={e.Social:F2} C={e.Control:F2} N={e.Novelty:F2} F={e.ContextFocus:F2}");
        }

        // 2. 更新 Guilt（独立通道）
        UpdateGuilt(e);

        // 3. 根据语义轴 + 当前情绪 + guilt + 记忆摘要(短/长) 计算新情绪
        ApplyEmotionDeltaFromEvent(e);

        // 4. 在这里同步到 Ink / UI / 其他系统（由你接入）
        // e.g. InkStory.variablesState["s_hope"] = _current.Hope;
    }

    /// <summary>
    /// 由外部生命系统调用，显式设置苏拉米斯是否存活。
    /// </summary>
    public void SetAlive(bool alive)
    {
        if (_isAlive == alive) return;
        _isAlive = alive;

        if (!alive && logEvents)
            Debug.Log("[PersonalitySystem] Sulamith is dead. Personality updates halted.");
    }

    #endregion

    #region Core Logic (Level 2: NN + Memory Summary)

    private void ApplyEmotionDeltaFromEvent(PersonalityEvent e)
    {
        Vector4 emo = CurrentVector;
        float now = Time.time;

        // 使用“当前历史”计算记忆摘要（注意：此时尚未加入本事件）
        Vector4 shortBias = GetShortTermBias(now);
        Vector4 longBias = GetLongTermBias();

        if (logMemory)
            Debug.Log($"[MemorySummary] ShortBias={shortBias}  LongBias={longBias}");

        // 交给映射函数：
        // - 若有 NN：输入 = emo + guilt + 身体 + 上下文 + Tag one-hot + shortBias + longBias
        // - 若无 NN：使用启发式 + (shortBias + longBias)
        // 事件本身的响应（不含任何记忆偏置）
        Vector4 baseDelta = EvaluateHeuristic(e, emo, guilt); // 若你后续要接 NN，也应在这里得到“事件响应”而非“含偏置输出”

        // ★ 记忆偏置（由历史生成）
        static float CombineNoFlip(float baseD, float bias)
        {
            // bias>0：倾向正向；bias<0：倾向负向
            // 同号增强，异号减弱，但不改变 baseD 的符号
            float k = 1f + Mathf.Clamp(Mathf.Abs(bias), 0f, 0.6f); // 1~1.6
            if (Mathf.Sign(baseD) == Mathf.Sign(bias) && baseD != 0f) return baseD * k;
            if (baseD != 0f) return baseD / k;
            return bias * 0.05f; // baseD=0 时给很弱的漂移（可改为 0）
        }

        Vector4 biasSum = shortBias + longBias;
        Vector4 finalDelta = new Vector4(
            CombineNoFlip(baseDelta.x, biasSum.x),
            CombineNoFlip(baseDelta.y, biasSum.y),
            CombineNoFlip(baseDelta.z, biasSum.z),
            CombineNoFlip(baseDelta.w, biasSum.w)
        );


        // ★ 记忆只记录“事件本身的响应”，不要记录“含偏置的最终输出”
        // 否则会形成自我强化回路：bias -> delta变大 -> 写回记忆 -> bias更大
        var mem = new MemoryEntry
        {
            Tag = e.Tag,
            Delta = baseDelta,
            Time = now
        };
        AddEpisodicMemory(mem);
        AddLongTermMemory(mem);

        // 最终用于更新情绪的 delta
        Vector4 delta = finalDelta;


        // 随机扰动
        if (noiseAmplitude > 0f)
        {
            delta += new Vector4(
                UnityEngine.Random.Range(-noiseAmplitude, noiseAmplitude),
                UnityEngine.Random.Range(-noiseAmplitude, noiseAmplitude),
                UnityEngine.Random.Range(-noiseAmplitude, noiseAmplitude),
                UnityEngine.Random.Range(-noiseAmplitude, noiseAmplitude)
            );
        }

        // 惯性平滑
        // 惯性平滑（带软边界：贴近 0/1 时，同向变化会自然变小，而不是直接“顶死”）
        float alpha = Mathf.Clamp01(1f - emotionInertia);

        static float ApplySoftCap(float x, float dx)
        {
            // x in [0,1]
            // 正向：越接近 1 越难再涨；负向：越接近 0 越难再降
            if (dx > 0f) dx *= (1f - x);
            else dx *= x;

            return x + dx;
        }

        Vector4 target = emo;
        target.x = ApplySoftCap(emo.x, delta.x);
        target.y = ApplySoftCap(emo.y, delta.y);
        target.z = ApplySoftCap(emo.z, delta.z);
        target.w = ApplySoftCap(emo.w, delta.w);

        Vector4 blended = Vector4.Lerp(emo, target, alpha);

        // 写回
        _current.Hope = Mathf.Clamp01(blended.x);
        _current.Happiness = Mathf.Clamp01(blended.y);
        _current.Trust = Mathf.Clamp01(blended.z);
        _current.Affinity = Mathf.Clamp01(blended.w);

    }

    /// <summary>
    /// Level 2 映射：优先使用 NN；缺省时回退到启发式。
    /// </summary>
    private Vector4 EvaluateNetworkLikeMapping(
        PersonalityEvent e,
        Vector4 emo,
        float guiltInput,
        Vector4 shortBias,
        Vector4 longBias)
    {
        // 若提供 NN 配置，使用 NN（记忆摘要作为输入特征，不再额外叠加到输出）
        if (_nn != null && _nn.IsValid)
        {
            float[] input = BuildInputVector(e, emo, guiltInput, shortBias, longBias);
            return _nn.Evaluate(input);
        }

        // 启发式回退：只返回“事件本身响应”（不含记忆偏置）
        // 记忆偏置在 ApplyEmotionDeltaFromEvent 中统一合成
        Vector4 baseDelta = EvaluateHeuristic(e, emo, guiltInput);
        return baseDelta;

    }

    /// <summary>
    /// 构造 NN 输入向量（Level 2 方案）：
    /// 当前情绪(4) + guilt(1) + 身体(3) + 上下文(5) + Tag one-hot + shortBias(4) + longBias(4)
    /// </summary>
    private float[] BuildInputVector(
        PersonalityEvent e,
        Vector4 emo,
        float guiltInput,
        Vector4 shortBias,
        Vector4 longBias)
    {
        int tagCount = Enum.GetValues(typeof(PersonalityEventTag)).Length;

        int inputSize =
            4 +     // emo
            1 +     // guilt
            3 +     // health/fatigue/stress
            5 +     // impact/tone/camp/day/lastContact
            tagCount +
            4 +     // shortBias
            4;      // longBias

        float[] x = new float[inputSize];
        int k = 0;

        // 当前情绪
        x[k++] = Mathf.Clamp01(emo.x);
        x[k++] = Mathf.Clamp01(emo.y);
        x[k++] = Mathf.Clamp01(emo.z);
        x[k++] = Mathf.Clamp01(emo.w);

        // guilt
        x[k++] = Mathf.Clamp01(guiltInput);

        // 身体
        x[k++] = Mathf.Clamp01(e.Health);
        x[k++] = Mathf.Clamp01(e.Fatigue);
        x[k++] = Mathf.Clamp01(e.Stress);

        // 上下文
        float impact = NormalizeImpact(e.Impact);
        x[k++] = impact;
        x[k++] = Mathf.Clamp(e.PlayerTone, -1f, 1f);
        x[k++] = Mathf.Clamp01(e.CampState);
        x[k++] = Mathf.Clamp01(e.DayNormalized);
        x[k++] = Mathf.Clamp01(e.TimeSinceLastContact);

        // Tag one-hot
        int tagIndex = (int)e.Tag;
        int tagCountLocal = tagCount;
        for (int i = 0; i < tagCountLocal; i++)
        {
            x[k++] = (i == tagIndex) ? 1f : 0f;
        }

        // 短期记忆摘要
        x[k++] = shortBias.x;
        x[k++] = shortBias.y;
        x[k++] = shortBias.z;
        x[k++] = shortBias.w;

        // 长期记忆摘要
        x[k++] = longBias.x;
        x[k++] = longBias.y;
        x[k++] = longBias.z;
        x[k++] = longBias.w;

        return x;
    }

    /// <summary>
    /// 原有启发式（不含记忆叠加），作为 NN 缺席时的基础映射。
    /// </summary>
    private Vector4 EvaluateHeuristic(PersonalityEvent e, Vector4 emo, float guiltInput)
    {
        float impact = NormalizeImpact(e.Impact);

        float V = Mathf.Clamp(e.Valence, -1f, 1f);
        float A = Mathf.Clamp(e.Agency, -1f, 1f);
        float M = Mathf.Clamp(e.Moral, -1f, 1f);
        float S = Mathf.Clamp(e.Social, -1f, 1f);
        float C = Mathf.Clamp01(e.Control);
        float N = Mathf.Clamp01(e.Novelty);
        float F = Mathf.Clamp01(e.ContextFocus);

        // Hope
        float dHope =
            0.18f * V * impact +
            0.22f * (C - 0.5f) * impact +
            0.16f * M * impact;

        // Happiness
        float dHappy =
            0.22f * V * impact +
            0.10f * N * V * impact;

        // Trust
        float dTrust = 0f;
        float playerResp = Mathf.Max(0f, A);
        dTrust += 0.18f * playerResp * Mathf.Max(0f, V) * F * impact;
        dTrust -= 0.22f * playerResp * Mathf.Max(0f, -V) * F * impact;
        dTrust -= 0.20f * playerResp * Mathf.Max(0f, -M) * F * impact;
        dTrust += 0.08f * F * S * impact;

        // Affinity
        float dAffinity =
            0.20f * S * F * impact +
            0.06f * V * F * impact;

        var d = new Vector4(dHope, dHappy, dTrust, dAffinity);

        // guilt 调制
        if (guiltInput > 0f)
        {
            float guiltPosFactor = 1f - 0.5f * guiltInput;
            float guiltNegBoost = 1f + 0.5f * guiltInput;

            if (d.x > 0) d.x *= guiltPosFactor;
            if (d.y > 0) d.y *= guiltPosFactor;

            if (d.x < 0) d.x *= guiltNegBoost;
            if (d.y < 0) d.y *= guiltNegBoost;
        }

        // 身体状态调制
        float health = e.Health < 0f ? 1f : Mathf.Clamp01(e.Health);
        float fatigue = e.Fatigue < 0f ? 0f : Mathf.Clamp01(e.Fatigue);
        float stress = e.Stress < 0f ? 0f : Mathf.Clamp01(e.Stress);

        if (health < 1f)
        {
            float lack = 1f - health;
            d.x -= 0.20f * lack;
            d.y -= 0.25f * lack;
        }

        if (fatigue > 0f)
        {
            float posScale = Mathf.Lerp(1f, 0.6f, fatigue);
            if (d.x > 0) d.x *= posScale;
            if (d.y > 0) d.y *= posScale;
            d.y -= 0.10f * fatigue;
        }

        if (stress > 0f)
        {
            float negBoost = 1f + 0.7f * stress;
            float posDamp = 1f - 0.4f * stress;

            if (d.x < 0) d.x *= negBoost;
            if (d.y < 0) d.y *= negBoost;

            if (d.x > 0) d.x *= posDamp;
            if (d.y > 0) d.y *= posDamp;

            float trustScale = Mathf.Lerp(1f, 0.9f, stress);
            d.z *= trustScale;
            d.w *= trustScale;
        }

        return d;
    }

    private static float NormalizeImpact(float impact)
    {
        if (impact <= 0f) return 0.7f;
        return Mathf.Clamp01(impact);
    }

    #endregion

    #region Memory Logic

    private void AddEpisodicMemory(MemoryEntry e)
    {
        _episodicMemories.Add(e);
        if (episodicCapacity > 0 && _episodicMemories.Count > episodicCapacity)
        {
            int removeCount = _episodicMemories.Count - episodicCapacity;
            _episodicMemories.RemoveRange(0, removeCount);
        }
    }

    private void AddLongTermMemory(MemoryEntry e)
    {
        if (!_longTermStats.TryGetValue(e.Tag, out var stat))
        {
            stat = new LongTermStat();
            _longTermStats[e.Tag] = stat;
        }

        stat.Count++;
        stat.Sum += e.Delta;
    }

    private Vector4 GetShortTermBias(float now)
    {
        if (shortTermWindow <= 0f || shortTermWeight <= 0f || _episodicMemories.Count == 0)
            return Vector4.zero;

        Vector4 acc = Vector4.zero;

        for (int i = _episodicMemories.Count - 1; i >= 0; i--)
        {
            var m = _episodicMemories[i];
            float dt = now - m.Time;
            if (dt < 0f) continue;
            if (dt > shortTermWindow) break;

            float w = 1f - dt / shortTermWindow;
            acc += m.Delta * w;
        }

        return acc * shortTermWeight;
    }

    private Vector4 GetLongTermBias()
    {
        if (_longTermStats.Count == 0 || longTermWeight <= 0f)
            return Vector4.zero;

        float hopeBias = 0f;
        float happyBias = 0f;
        float trustBias = 0f;
        float affinityBias = 0f;

        foreach (var kv in _longTermStats)
        {
            var tag = kv.Key;
            var stat = kv.Value;
            if (stat.Count == 0) continue;

            Vector4 avg = stat.Sum / stat.Count;

            switch (tag)
            {
                case PersonalityEventTag.Player_Comfort:
                case PersonalityEventTag.Player_Encourage:
                case PersonalityEventTag.Player_Apologize:
                case PersonalityEventTag.Player_KeepPromise:
                case PersonalityEventTag.Meta_FrequentCheckIn:
                case PersonalityEventTag.Meta_UseResourceForHer:
                    trustBias += avg.z * 0.4f;
                    affinityBias += avg.w * 0.6f;
                    hopeBias += avg.x * 0.2f;
                    happyBias += avg.y * 0.1f;
                    break;

                case PersonalityEventTag.Player_BreakPromise:
                case PersonalityEventTag.Player_LieDetected:
                case PersonalityEventTag.Meta_IgnoreHerNeed:
                case PersonalityEventTag.Meta_TalkOnlyWhenNeed:
                case PersonalityEventTag.Player_Objectify:
                    trustBias += avg.z * 0.8f;
                    affinityBias += avg.w * 0.6f;
                    happyBias += avg.y * 0.1f;
                    break;

                case PersonalityEventTag.Camp_LossDueToDecision_Player:
                case PersonalityEventTag.Camp_Casualty:
                    hopeBias += avg.x * 0.4f;
                    break;
            }
        }

        hopeBias = Mathf.Clamp(hopeBias, -0.25f, 0.25f);
        happyBias = Mathf.Clamp(happyBias, -0.20f, 0.20f);
        trustBias = Mathf.Clamp(trustBias, -0.35f, 0.35f);
        affinityBias = Mathf.Clamp(affinityBias, -0.35f, 0.35f);

        Vector4 bias = new Vector4(hopeBias, happyBias, trustBias, affinityBias);
        return bias * longTermWeight;
    }

    #endregion

    #region Semantic Axes & Guilt

    private void ComputeSemanticAxes(ref PersonalityEvent e)
    {
        //-1,1
        e.Valence = 0f;
        e.Agency = 0f;
        e.Moral = 0f;
        e.Social = 0f;

        //0,1
        e.Control = 0.5f;
        e.Novelty = 0.3f;
        e.ContextFocus = 0.5f;

        float im = NormalizeImpact(e.Impact);

        switch (e.Tag)
        {
            // === 玩家态度 ===
            case PersonalityEventTag.Player_Comfort:
                e.Valence = +0.8f; e.Agency = +1f; e.Moral = +0.4f; e.Social = +1f;
                e.Control = 0.8f; e.ContextFocus = 1f;
                break;

            case PersonalityEventTag.Player_Encourage:
                e.Valence = +0.7f; e.Agency = +1f; e.Moral = +0.4f; e.Social = +0.6f;
                e.Control = 0.9f; e.ContextFocus = 1f;
                break;

            case PersonalityEventTag.Player_Apologize:
                e.Valence = +0.3f; e.Agency = +1f; e.Moral = +0.6f; e.Social = +0.6f;
                e.Control = 0.6f; e.ContextFocus = 1f;
                break;

            case PersonalityEventTag.Player_Harsh:
                e.Valence = -0.8f; e.Agency = +1f; e.Moral = -0.3f; e.Social = -0.8f;
                e.Control = 0.7f; e.ContextFocus = 1f;
                break;

            case PersonalityEventTag.Player_Ignorant:
                e.Valence = -0.4f; e.Agency = +1f; e.Social = -0.5f;
                e.ContextFocus = 1f;
                break;

            case PersonalityEventTag.Player_Objectify:
                e.Valence = -0.7f; e.Agency = +1f; e.Moral = -0.4f; e.Social = -0.9f;
                e.ContextFocus = 1f;
                break;

            // === 承诺 / 诚信 ===
            case PersonalityEventTag.Player_KeepPromise:
                e.Valence = +0.8f; e.Agency = +1f; e.Moral = +0.8f; e.Social = +0.7f;
                e.ContextFocus = 1f;
                break;

            case PersonalityEventTag.Player_BreakPromise:
            case PersonalityEventTag.Player_LieDetected:
                e.Valence = -0.9f; e.Agency = +1f; e.Moral = -0.8f; e.Social = -0.7f;
                e.ContextFocus = 1f;
                break;

            case PersonalityEventTag.Player_Transparency:
                e.Valence = +0.2f; e.Agency = +1f; e.Moral = +0.5f; e.Social = +0.2f;
                e.ContextFocus = 1f;
                break;

            // === 营地资源 & 安全 ===
            case PersonalityEventTag.Camp_ResourceUp:
                e.Valence = +0.6f; e.Control = 0.8f; e.ContextFocus = 0f;
                break;

            case PersonalityEventTag.Camp_ResourceDown:
                e.Valence = -0.6f; e.Control = 0.3f; e.ContextFocus = 0f;
                break;

            case PersonalityEventTag.Camp_CriticalShortage:
                e.Valence = -0.9f; e.Control = 0.1f; e.Novelty = 0.7f; e.ContextFocus = 0f;
                break;

            case PersonalityEventTag.Camp_SecurityImproved:
                e.Valence = +0.5f; e.Control = 0.8f; e.ContextFocus = 0f;
                break;

            case PersonalityEventTag.Camp_SecurityBreach:
                e.Valence = -0.7f; e.Control = 0.2f; e.Novelty = 0.7f; e.ContextFocus = 0f;
                break;

            // === 人员 ===
            case PersonalityEventTag.Camp_Casualty:
                e.Valence = -1f; e.Control = 0.1f; e.Moral = -0.4f; e.ContextFocus = 0.4f;
                break;

            case PersonalityEventTag.Camp_Injury:
                e.Valence = -0.6f; e.Control = 0.3f; e.ContextFocus = 0.4f;
                break;

            case PersonalityEventTag.Camp_SuccessfulRescue:
                e.Valence = +0.9f; e.Moral = +0.8f; e.Control = 0.7f;
                e.Novelty = 0.8f; e.ContextFocus = 0.6f;
                break;

            case PersonalityEventTag.Camp_LossDueToDecision_Player:
                e.Valence = -1f; e.Agency = +1f; e.Moral = -0.6f;
                e.Control = 0.2f; e.ContextFocus = 0.6f;
                break;

            case PersonalityEventTag.Camp_LossAvoidedByPlayer:
                e.Valence = +0.8f; e.Agency = +1f; e.Moral = +0.7f;
                e.Control = 0.8f; e.ContextFocus = 0.5f;
                break;

            // === 天气 / 阶段 ===
            case PersonalityEventTag.Weather_StormStart:
                e.Valence = -0.4f; e.Control = 0.3f; e.Novelty = 0.6f; e.ContextFocus = 0f;
                break;

            case PersonalityEventTag.Weather_StormPeak:
                e.Valence = -0.7f; e.Control = 0.1f; e.Novelty = 0.9f; e.ContextFocus = 0f;
                break;

            case PersonalityEventTag.Weather_StormEnd:
                e.Valence = +0.7f; e.Control = 0.7f; e.Novelty = 0.6f; e.ContextFocus = 0f;
                break;

            case PersonalityEventTag.Milestone_FinalStorm:
                e.Valence = -0.6f; e.Novelty = 0.9f; e.Control = 0.2f; e.ContextFocus = 0.3f;
                break;

            // === 元事件：关系 / 陪伴 ===
            case PersonalityEventTag.Meta_LongNoContact:
                e.Valence = -0.4f; e.Social = -0.7f; e.ContextFocus = 1f;
                break;

            case PersonalityEventTag.Meta_FrequentCheckIn:
                e.Valence = +0.4f; e.Social = +0.8f; e.ContextFocus = 1f;
                break;

            case PersonalityEventTag.Meta_UseResourceForHer:
                e.Valence = +0.5f; e.Social = +0.9f; e.Moral = +0.2f; e.ContextFocus = 1f;
                break;

            case PersonalityEventTag.Meta_TalkOnlyWhenNeed:
                e.Valence = -0.2f; e.Social = -0.4f; e.ContextFocus = 1f;
                break;
        }

    }

    private void UpdateGuilt(PersonalityEvent e)
    {
        float impact = NormalizeImpact(e.Impact);

        switch (e.Tag)
        {
            case PersonalityEventTag.Camp_LossDueToDecision_Player:
                guilt += 0.25f * impact;
                break;

            case PersonalityEventTag.Camp_Casualty:
                guilt += 0.12f * impact;
                break;

            case PersonalityEventTag.Meta_UseResourceForHer:
                if (e.CampState > 0f && e.CampState < 0.3f)
                    guilt += 0.10f * impact;
                break;

            // 缓解内疚
            case PersonalityEventTag.Player_Comfort:
            case PersonalityEventTag.Player_Encourage:
            case PersonalityEventTag.Player_Apologize:
            case PersonalityEventTag.Camp_SuccessfulRescue:
                guilt -= 0.10f * impact;
                break;
        }

        guilt = Mathf.Clamp01(guilt);
    }

    #endregion
}

/// <summary>
/// 前馈网络配置：通过 ScriptableObject 存储权重。
/// </summary>
[CreateAssetMenu(
    fileName = "PersonalityNNConfig",
    menuName = "ProjectSulamith/Personality NN Config")]
public class PersonalityNNConfig : ScriptableObject
{
    public int inputSize;
    public int hiddenSize = 32;
    public int outputSize = 4; // ΔHope, ΔHappiness, ΔTrust, ΔAffinity

    [Header("Layer 1")]
    public float[] w1; // length = inputSize * hiddenSize
    public float[] b1; // length = hiddenSize

    [Header("Layer 2")]
    public float[] w2; // length = hiddenSize * outputSize
    public float[] b2; // length = outputSize

    public bool IsValid()
    {
        return
            w1 != null && b1 != null &&
            w2 != null && b2 != null &&
            w1.Length == inputSize * hiddenSize &&
            b1.Length == hiddenSize &&
            w2.Length == hiddenSize * outputSize &&
            b2.Length == outputSize;
    }
}

/// <summary>
/// 运行时前馈网络，仅做推理，不包含训练逻辑。
/// </summary>
public class PersonalityNN
{
    private readonly PersonalityNNConfig _cfg;

    public bool IsValid => _cfg != null && _cfg.IsValid();

    public PersonalityNN(PersonalityNNConfig cfg)
    {
        _cfg = cfg;
    }

    public Vector4 Evaluate(float[] input)
    {
        if (!IsValid || input == null || input.Length != _cfg.inputSize)
            return Vector4.zero;

        int inSize = _cfg.inputSize;
        int hSize = _cfg.hiddenSize;
        int oSize = _cfg.outputSize;

        // hidden = ReLU(W1 * x + b1)
        float[] h = new float[hSize];
        int idx = 0;
        for (int i = 0; i < hSize; i++)
        {
            float sum = _cfg.b1[i];
            for (int j = 0; j < inSize; j++)
            {
                sum += _cfg.w1[idx++] * input[j];
            }
            h[i] = Mathf.Max(0f, sum);
        }

        // output = tanh(W2 * h + b2)，限制在 [-1,1] 当作 Δ
        float[] o = new float[oSize];
        idx = 0;
        for (int i = 0; i < oSize; i++)
        {
            float sum = _cfg.b2[i];
            for (int j = 0; j < hSize; j++)
            {
                sum += _cfg.w2[idx++] * h[j];
            }
            o[i] = Tanh(sum);
        }

        return new Vector4(o[0], o[1], o[2], o[3]);
    }

    private static float Tanh(float x)
    {
        // 稍微稳一点的 tanh 实现
        float ex = Mathf.Exp(2f * Mathf.Clamp(x, -10f, 10f));
        return (ex - 1f) / (ex + 1f);
    }
}
