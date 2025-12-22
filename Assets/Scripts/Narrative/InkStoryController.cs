using System;
using System.Collections.Generic;
using Ink.Runtime;
using UnityEngine;

/// <summary>
/// 极简 Ink 控制器：负责加载 Story、推进剧情、绑定 External Functions。
/// </summary>
public class InkStoryController : MonoBehaviour
{
    [Header("Ink JSON")]
    public TextAsset inkJSONAsset;

    public Story Story { get; private set; }

    public event Action<string> OnLine;              // 每句台词回调（可接 UI）
    public event Action<List<Choice>> OnChoices;     // 选项回调（测试 HUD 用）

    private void Awake()
    {
        InitStory();
    }

    public void InitStory()
    {
        if (inkJSONAsset == null)
        {
            Debug.LogError("[InkStoryController] Ink JSON 未绑定。");
            return;
        }

        Story = new Story(inkJSONAsset.text);
        BindExternalFunctions();
    }

    public void StartStory(string knot = "start")
    {
        if (Story == null) InitStory();
        if (Story == null) return;

        if (!string.IsNullOrEmpty(knot))
            Story.ChoosePathString(knot);

        Continue();
    }

    public void Continue()
    {
        if (Story == null) return;

        while (Story.canContinue)
        {
            var line = Story.Continue().Trim();
            if (!string.IsNullOrEmpty(line))
            {
                OnLine?.Invoke(line);
                Debug.Log("[Ink] " + line);
            }

            // 这里也可以读取 Story.currentTags 做演出控制
        }

        var choices = Story.currentChoices;
        OnChoices?.Invoke(choices);
        if (choices.Count > 0)
        {
            for (int i = 0; i < choices.Count; i++)
                Debug.Log($"[Ink Choice {i}] {choices[i].text}");
        }
    }

    public void ChooseOption(int index)
    {
        if (Story == null) return;
        if (index < 0 || index >= Story.currentChoices.Count) return;

        Story.ChooseChoiceIndex(index);
        Continue();
    }

    private void BindExternalFunctions()
    {
        if (Story == null) return;

        // 1) 触发人格事件：~ ps_event("Player_Comfort", 0.8)
        Story.BindExternalFunction("ps_event", (string tagName, float impact) =>
        {
            if (PersonalitySystem.Instance == null)
            {
                Debug.LogWarning("[Ink] PersonalitySystem 未初始化。");
                return;
            }

            if (!Enum.TryParse(tagName, out PersonalitySystem.PersonalityEventTag tag))
            {
                Debug.LogWarning("[Ink] 未知 PersonalityEventTag: " + tagName);
                return;
            }

            var e = new PersonalitySystem.PersonalityEvent
            {
                Tag = tag,
                Impact = Mathf.Clamp01(impact <= 0 ? 0.7f : impact),
                PlayerTone = 0f,
                CampState = 0.5f,
                DayNormalized = 0.5f,
                TimeSinceLastContact = 0.2f,
                Health = HealthSystem.Instance != null ? HealthSystem.Instance.Snapshot.Vitality : 1f,
                Fatigue = HealthSystem.Instance != null ? HealthSystem.Instance.Snapshot.Fatigue : 0f,
                Stress = 0f
            };

            PersonalitySystem.Instance.RaiseEvent(e);
        });

        // 2) 触发健康事件：~ hs_event("Damage", 0.2)
        Story.BindExternalFunction("hs_event", (string typeName, float amount) =>
        {
            if (HealthSystem.Instance == null)
            {
                Debug.LogWarning("[Ink] HealthSystem 未初始化。");
                return;
            }

            if (!Enum.TryParse(typeName, out HealthSystem.HealthEventType type))
            {
                Debug.LogWarning("[Ink] 未知 HealthEventType: " + typeName);
                return;
            }

            var e = new HealthSystem.HealthEvent
            {
                Type = type,
                Amount = amount,
                DurationHours = 0,
                SourceTag = "ink_test"
            };

            HealthSystem.Instance.ApplyEvent(e);
        });

        // 3) 简单日志：~ log("xxx")
        Story.BindExternalFunction("log", (string msg) =>
        {
            Debug.Log("[InkLog] " + msg);
        });
    }
}
