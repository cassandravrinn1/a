using System;
using System.Collections;
using System.Collections.Generic;
using Ink.Runtime;
using UnityEngine;

namespace ProjectSulamith.Dialogue
{
    /// <summary>
    /// Ink 管理器（命令驱动版）：
    /// - Ink 只输出文本与 tags
    /// - InkManager 监测 tags 中的命令并执行（人格/健康）
    ///
    /// Ink 写法示例（tag）：
    ///   你看起来很累。
    ///   #cmd: hs_event AddFatigue 0.3
    ///   #cmd: hs_activity HardWork
    ///
    ///   我会尽量照顾你。
    ///   #cmd: ps_event Player_Comfort 0.8
    ///
    /// 支持命令：
    ///   - ps_event <PersonalityEventTag> <impact>
    ///   - hs_event <HealthEventType> <amount>
    ///   - hs_sleep <true/false>
    ///   - hs_activity <ActivityLevel>
    ///
    /// 注意：本版不需要在 Ink 里声明 external function，也不要求 Ink 声明 ps_hope 等变量。
    /// </summary>
    public class InkManager : MonoBehaviour
    {
        [Header("Ink JSON")]
        public TextAsset inkJSONAsset;

        public Story StoryInstance { get; private set; }

        public event Action<string> OnLine;
        public event Action<List<Choice>> OnChoices;
        public event Action OnStoryEnded;

        [Header("Command Tags")]
        [Tooltip("识别命令用的 tag 前缀。Ink 里写：#cmd: hs_event Damage 0.2")]
        public string commandTagPrefix = "cmd:";

        [Tooltip("命令执行日志")]
        public bool logCommands = true;

        [Tooltip("遇到未知命令时输出 Warning")]
        public bool warnUnknownCommands = true;

        [Header("Safety")]
        [Tooltip("防止 Ink 里写了大量命令导致某帧执行过多。0 表示不限制。")]
        public int maxCommandsPerContinueStep = 32;

        private bool _initialized = false;
        private bool _isContinuing = false;

        private void Awake()
        {
            Initialize();
        }

        public void Initialize()
        {
            if (_initialized) return;

            if (inkJSONAsset == null)
            {
                Debug.LogError("[InkManager] inkJSONAsset 未指定！");
                return;
            }

            StoryInstance = new Story(inkJSONAsset.text);
            _initialized = true;

            Debug.Log("[InkManager] 初始化完成（命令驱动版）。");
        }

        public void StartStory(string knot = null)
        {
            if (!_initialized) Initialize();
            if (StoryInstance == null) return;

            if (!string.IsNullOrEmpty(knot))
                StoryInstance.ChoosePathString(knot);

            ContinueStory();
        }

        public void ContinueStory()
        {
            if (StoryInstance == null)
            {
                Debug.LogError("[InkManager] StoryInstance 为空！");
                return;
            }

            if (_isContinuing)
            {
                Debug.LogWarning("[InkManager] ContinueStory 重入已阻止。");
                return;
            }

            _isContinuing = true;
            try
            {
                while (StoryInstance.canContinue)
                {
                    // Ink 推进一行
                    string line = StoryInstance.Continue()?.Trim();

                    // ★ 执行本行附带 tags 中的命令
                    ExecuteCommandTags();

                    // 输出正常文本
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        Debug.Log($"[InkManager] Line: {line}");
                        OnLine?.Invoke(line);
                    }
                }

                // 输出选项
                var choices = StoryInstance.currentChoices;
                if (choices != null && choices.Count > 0)
                {
                    OnChoices?.Invoke(new List<Choice>(choices));
                }
                else
                {
                    if (!StoryInstance.canContinue)
                    {
                        OnChoices?.Invoke(new List<Choice>());
                        OnStoryEnded?.Invoke();
                    }
                }
            }
            finally
            {
                _isContinuing = false;
            }
        }

        /// <summary>
        /// 玩家选择某个选项
        /// </summary>
        public void ChooseOption(int index)
        {
            if (StoryInstance == null)
            {
                Debug.LogError("[InkManager] StoryInstance 为空！");
                return;
            }

            var choices = StoryInstance.currentChoices;
            if (choices == null || choices.Count == 0)
            {
                Debug.LogWarning("[InkManager] 当前没有可选选项。");
                return;
            }

            if (index < 0 || index >= choices.Count)
            {
                Debug.LogWarning($"[InkManager] 选项索引越界: {index}");
                return;
            }

            StoryInstance.ChooseChoiceIndex(index);

            // 延迟一帧防止 UI 点击/回调重入（保留你原逻辑）
            StartCoroutine(ContinueNextFrame());
        }

        private IEnumerator ContinueNextFrame()
        {
            yield return null;
            ContinueStory();
        }

        // =====================================================================
        // 命令解析与执行
        // =====================================================================

        private void ExecuteCommandTags()
        {
            if (StoryInstance == null) return;

            var tags = StoryInstance.currentTags;
            if (tags == null || tags.Count == 0) return;

            int executed = 0;

            for (int i = 0; i < tags.Count; i++)
            {
                string raw = tags[i];
                if (string.IsNullOrWhiteSpace(raw)) continue;

                // Ink tag 可能带 '#'
                string t = raw.Trim();
                if (t.StartsWith("#")) t = t.Substring(1).Trim();

                // 必须是 cmd: 前缀
                if (!t.StartsWith(commandTagPrefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                string payload = t.Substring(commandTagPrefix.Length).Trim();
                if (string.IsNullOrEmpty(payload)) continue;

                if (maxCommandsPerContinueStep > 0 && executed >= maxCommandsPerContinueStep)
                {
                    Debug.LogWarning($"[InkCmd] Exceeded maxCommandsPerContinueStep={maxCommandsPerContinueStep}, remaining commands skipped.");
                    return;
                }

                executed++;

                if (logCommands) Debug.Log($"[InkCmd] {payload}");

                // 解析：cmd arg1 arg2 ...
                var parts = payload.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) continue;

                string cmd = parts[0];

                try
                {
                    switch (cmd)
                    {
                        case "ps_event":
                            // ps_event <PersonalityEventTag> <impact>
                            HandlePsEvent(parts);
                            break;

                        case "hs_event":
                            // hs_event <HealthEventType> <amount>
                            HandleHsEvent(parts);
                            break;

                        case "hs_sleep":
                            // hs_sleep <true/false>
                            HandleHsSleep(parts);
                            break;

                        case "hs_activity":
                            // hs_activity <ActivityLevel>
                            HandleHsActivity(parts);
                            break;

                        default:
                            if (warnUnknownCommands)
                                Debug.LogWarning($"[InkCmd] Unknown command: {cmd} (raw='{payload}')");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[InkCmd] Execute failed: '{payload}'  ex={ex}");
                }
            }
        }

        private void HandlePsEvent(string[] parts)
        {
            if (parts.Length < 3)
            {
                Debug.LogWarning("[InkCmd] ps_event requires: ps_event <PersonalityEventTag> <Impact>");
                return;
            }

            var ps = PersonalitySystem.Instance;
            if (ps == null)
            {
                Debug.LogWarning("[InkCmd] PersonalitySystem.Instance is null");
                return;
            }

            if (!Enum.TryParse(parts[1], out PersonalitySystem.PersonalityEventTag tag))
            {
                Debug.LogWarning("[InkCmd] Unknown PersonalityEventTag: " + parts[1]);
                return;
            }

            if (!float.TryParse(parts[2], out float impact))
            {
                Debug.LogWarning("[InkCmd] Invalid impact: " + parts[2]);
                return;
            }

            var hs = HealthSystem.Instance;
            var h = hs != null ? hs.Snapshot : default;

            // 用健康状态给人格事件的身体输入（用于调制）
            var e = new PersonalitySystem.PersonalityEvent
            {
                Tag = tag,
                Impact = Mathf.Clamp01(impact <= 0 ? 0.7f : impact),

                // 你可后续扩展：从 tag 里再读 tone/camp/day 等参数
                PlayerTone = 0f,
                CampState = 0.5f,
                DayNormalized = 0.5f,
                TimeSinceLastContact = 0.2f,

                Health = hs != null ? h.Vitality : 1f,
                Fatigue = hs != null ? h.Fatigue : 0f,
                Stress = 0f
            };

            ps.RaiseEvent(e);
        }

        private void HandleHsEvent(string[] parts)
        {
            if (parts.Length < 3)
            {
                Debug.LogWarning("[InkCmd] hs_event requires: hs_event <HealthEventType> <Amount>");
                return;
            }

            var hs = HealthSystem.Instance;
            if (hs == null)
            {
                Debug.LogWarning("[InkCmd] HealthSystem.Instance is null");
                return;
            }

            if (!Enum.TryParse(parts[1], out HealthSystem.HealthEventType type))
            {
                Debug.LogWarning("[InkCmd] Unknown HealthEventType: " + parts[1]);
                return;
            }

            if (!float.TryParse(parts[2], out float amount))
            {
                Debug.LogWarning("[InkCmd] Invalid amount: " + parts[2]);
                return;
            }

            hs.ApplyEvent(new HealthSystem.HealthEvent
            {
                Type = type,
                Amount = amount,
                DurationHours = 0f,
                SourceTag = "ink_cmd"
            });
        }

        private void HandleHsSleep(string[] parts)
        {
            if (parts.Length < 2)
            {
                Debug.LogWarning("[InkCmd] hs_sleep requires: hs_sleep <true/false>");
                return;
            }

            var hs = HealthSystem.Instance;
            if (hs == null)
            {
                Debug.LogWarning("[InkCmd] HealthSystem.Instance is null");
                return;
            }

            if (!bool.TryParse(parts[1], out bool sleeping))
            {
                Debug.LogWarning("[InkCmd] Invalid bool: " + parts[1]);
                return;
            }

            hs.SetSleeping(sleeping);
        }

        private void HandleHsActivity(string[] parts)
        {
            if (parts.Length < 2)
            {
                Debug.LogWarning("[InkCmd] hs_activity requires: hs_activity <ActivityLevel>");
                return;
            }

            var hs = HealthSystem.Instance;
            if (hs == null)
            {
                Debug.LogWarning("[InkCmd] HealthSystem.Instance is null");
                return;
            }

            if (!Enum.TryParse(parts[1], out HealthSystem.ActivityLevel level))
            {
                Debug.LogWarning("[InkCmd] Unknown ActivityLevel: " + parts[1]);
                return;
            }

            hs.SetActivity(level);
        }
    }
}
