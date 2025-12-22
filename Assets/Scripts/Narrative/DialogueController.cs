using UnityEngine;
using System;

/// <summary>
/// 大模型参与人格系统的 3 种方式：
/// 0 = 不使用（纯启发式 / 小模型）
/// 1 = 完全覆盖（大模型直接给出 ΔEmotion）
/// 2 = 作为修正层（Additive Correction）
/// 3 = 与原解混合（Blend）
/// </summary>
public enum LargeModelMode
{
    Disabled = 0,           // 不使用大模型
    OverrideAll = 1,        // 大模型直接产生最终结果
    AdditiveCorrection = 2, // 大模型作为校正项：base + scale * llm
    BlendWithBase = 3       // baseDelta 与 llmDelta 插值混合
}

/// <summary>
/// 大模型桥接基类：
///
/// 负责：
/// - 把事件 + 当前情绪 + 记忆偏置组装成上下文
/// - 调用大模型（由子类实现）
/// - 返回情绪 Δ 以及可选的“日常对话”文本
///
/// 使用方式：
/// 1. 新建一个继承此类的 ScriptableObject（如 MySulamithLLMBridge）
/// 2. 重写 TryEvaluateWithDialogue(...)，在里面调用大模型：
///    - 让大模型同时输出 ΔHope/ΔHappy/ΔTrust/ΔAffinity + 一句对话
/// 3. 将该 ScriptableObject 挂在 PersonalitySystem.largeModelBridge 上
///别用错了！
/// 注意：
/// - PersonalitySystem 当前只必需 ΔEmotion；
/// - 对话可以其他系统单独调用这个 Bridge 获取，避免重复请求。
/// </summary>
public abstract class PersonalityLargeModelBridge : ScriptableObject
{
    /// <summary>
    /// 情绪 & 对话的联合结果结构。
    /// 
    /// </summary>
    [System.Serializable]
    public struct LLMEmotionAndDialogue
    {
        public Vector4 delta;   // ΔHope, ΔHappiness, ΔTrust, ΔAffinity
        public string reply;    // 推荐的日常对话文本（可为空）

        public LLMEmotionAndDialogue(Vector4 d, string r)
        {
            delta = d;
            reply = r;
        }
    }

    // =====================================================================
    // 核心接口 1： PersonalitySystem （只关心情绪 Δ）
    // =====================================================================

    /// <summary>
    /// PersonalitySystem 当前调用的接口：
    /// 仅获取“最终 ΔEmotion”，不关心对话内容。
    ///
    /// 默认实现：调用 TryEvaluateWithDialogue(...)，然后丢弃 reply。
    ///
    /// 返回：
    /// true  → 使用 finalDelta
    /// false → 使用回退（baseDelta）
    /// </summary>
    public virtual bool TryEvaluate(
        PersonalitySystem.PersonalityEvent e,
        Vector4 emo,
        float guiltInput,
        Vector4 shortBias,
        Vector4 longBias,
        Vector4 baseDelta,
        out Vector4 finalDelta)
    {
        // 调用带对话版本，但忽略对话内容
        if (TryEvaluateWithDialogue(e, emo, guiltInput, shortBias, longBias, baseDelta,
            out var result))
        {
            finalDelta = result.delta;
            return true;
        }

        finalDelta = baseDelta;
        return false;
    }

    // =====================================================================
    // 核心接口 2：给“对话/演出系统”用的（情绪 + 文本一起拿）
    // =====================================================================

    /// <summary>
    /// 真正实现 LLM 调用的接口：
    /// 一次调用同时返回：
    /// - 情绪 Δ（result.delta）
    /// - 可选的“苏拉米斯应当说的话”（result.reply）
    ///
    /// 使用场景：
    /// - 对话系统 / 演出系统可以在玩家做出某个选择后，
    ///   直接调用这个接口，拿到“她的心情变化”和“她会说什么”。
    ///
    /// 默认实现：什么也不做，返回 false。
    /// 在子类里 override 它即可。
    /// </summary>
    public virtual bool TryEvaluateWithDialogue(
        PersonalitySystem.PersonalityEvent e,
        Vector4 emo,
        float guiltInput,
        Vector4 shortBias,
        Vector4 longBias,
        Vector4 baseDelta,
        out LLMEmotionAndDialogue result)
    {
        // 默认：不使用大模型
        result = new LLMEmotionAndDialogue(baseDelta, string.Empty);
        return false;
    }



    /// <summary>
    /// 工具函数：构建完整“上下文描述”，以便你在 Prompt 中使用。
    /// 直接把这段 summary 拼进大模型的输入，让它理解当前局势。
    /// </summary>
    protected string BuildHumanReadableSummary(
        PersonalitySystem.PersonalityEvent e,
        Vector4 emo,
        float guiltInput,
        Vector4 shortBias,
        Vector4 longBias,
        Vector4 baseDelta)
    {
        return
            $"========= Sulamith Personality Event Summary =========\n\n" +

            "[Event]\n" +
            $"- Tag: {e.Tag}\n" +
            $"- Impact: {e.Impact:0.00}\n" +
            $"- PlayerTone: {e.PlayerTone:0.00}\n" +
            $"- CampState: {e.CampState:0.00}\n" +
            $"- DayNormalized: {e.DayNormalized:0.00}\n" +
            $"- TimeSinceLastContact: {e.TimeSinceLastContact:0.00}\n" +
            $"- Health={e.Health:0.00}, Fatigue={e.Fatigue:0.00}, Stress={e.Stress:0.00}\n" +
            $"- SemanticAxes: V={e.Valence:0.00} A={e.Agency:0.00} " +
            $"M={e.Moral:0.00} S={e.Social:0.00} C={e.Control:0.00} " +
            $"N={e.Novelty:0.00} F={e.ContextFocus:0.00}\n\n" +

            "[Current Emotion]\n" +
            $"- Hope={emo.x:0.00}\n" +
            $"- Happiness={emo.y:0.00}\n" +
            $"- Trust={emo.z:0.00}\n" +
            $"- Affinity={emo.w:0.00}\n\n" +

            "[Memory]\n" +
            $"- ShortTermBias: {shortBias}\n" +
            $"- LongTermBias: {longBias}\n\n" +

            "[Base Delta]\n" +
            $"{baseDelta}\n\n" +

            "======================================================\n";
    }
}



[CreateAssetMenu(
    fileName = "ExampleSulamithLLMBridge",
    menuName = "ProjectSulamith/Personality LLM Bridge")]
public class ExampleSulamithLLMBridge : PersonalityLargeModelBridge
{
    [TextArea(4, 12)]
    public string systemPrompt =
        "你是《机器、雨幕和少女》中苏拉米斯人格系统的情绪与对话推理模块。" +
        "根据提供的事件摘要和启发式结果，推断苏拉米斯在 Hope/Happiness/Trust/Affinity 四个维度上的变化，" +
        "并依据给出的上下文生成一句她在当前情境下可能会说出的中文回应。并给出三个可能的回复选项，并依照输入标准给出对应事件参数。";

    /// <summary>

    /// </summary>
    public override bool TryEvaluateWithDialogue(
        PersonalitySystem.PersonalityEvent e,
        Vector4 emo,
        float guiltInput,
        Vector4 shortBias,
        Vector4 longBias,
        Vector4 baseDelta,
        out LLMEmotionAndDialogue result)
    {

        result = new LLMEmotionAndDialogue(baseDelta, string.Empty);
        return false;
    }
}
