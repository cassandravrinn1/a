using System.Collections.Generic;
using Ink.Runtime;
using UnityEngine;

/// <summary>
/// 用 OnGUI 做一个极简测试面板，不进正式 UI 系统。
/// </summary>
public class InkPH_TestHUD : MonoBehaviour
{
    public InkStoryController ink;
    public InkVariableBridge bridge;

    private List<Choice> _currentChoices = new List<Choice>();
    private readonly List<string> _lines = new List<string>();

    private void Awake()
    {
        if (ink == null) ink = GetComponent<InkStoryController>();
        if (bridge == null) bridge = GetComponent<InkVariableBridge>();

        ink.OnLine += l => _lines.Add(l);
        ink.OnChoices += choices =>
        {
            _currentChoices = new List<Choice>(choices);
        };
    }

    private void Start()
    {
        // 开局：把系统状态推给 Ink，然后从 start 跑
        bridge.SyncGameToInk();
        ink.StartStory("start");
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 480, 600), GUI.skin.box);

        GUILayout.Label("== Personality & Health 测试面板 ==");

        // 显示当前人格
        if (PersonalitySystem.Instance != null)
        {
            var e = PersonalitySystem.Instance.Current;
            GUILayout.Label($"Hope: {e.Hope:F2}  Trust: {e.Trust:F2}");
            GUILayout.Label($"Guilt: {PersonalitySystem.Instance.guilt:F2}");
        }
        else GUILayout.Label("PersonalitySystem 未找到");

        // 显示当前健康
        if (HealthSystem.Instance != null)
        {
            var h = HealthSystem.Instance.Snapshot;
            GUILayout.Label($"Vitality: {h.Vitality:F2}  State: {h.HealthStateTag}  Risk: {h.RiskLevel}");
        }
        else GUILayout.Label("HealthSystem 未找到");

        GUILayout.Space(10);
        GUILayout.Label("最近台词：");
        for (int i = Mathf.Max(0, _lines.Count - 5); i < _lines.Count; i++)
            GUILayout.Label(_lines[i]);

        GUILayout.Space(10);
        GUILayout.Label("选项：");
        for (int i = 0; i < _currentChoices.Count; i++)
        {
            if (GUILayout.Button(_currentChoices[i].text))
            {
                ink.ChooseOption(i);
                // 事件触发后，Health/Personality 已被更新，HUD 会在下一帧显示新值
            }
        }

        GUILayout.EndArea();
    }
}
