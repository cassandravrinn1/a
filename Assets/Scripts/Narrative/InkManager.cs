/*
 * Public API (InkManager)
 * =======================
 *
 * Core Lifecycle
 *   - Initialize()
 *       初始化 StoryInstance（基于 inkJSONAsset）。Awake() 会自动调用；手动调用可做重入保护。
 *
 *   - StartStory(string knot = null)
 *       从指定 knot 开始（knot 为空则从当前 Story 的当前位置继续/默认入口）。
 *
 *   - ContinueStory()
 *       推进故事：逐行 Continue，同时解析并执行当前行 tags 中的 #cmd: 指令。
 *       若推进后存在选项，触发 OnChoices；否则触发 OnStoryEnded。
 *
 * Player Interaction
 *   - ChooseOption(int index)
 *       玩家选择选项并推进（内部延迟一帧 Continue）。
 *       若已装填 plan_arm，则会取消该 PlanId 下 CancelOnReply=true 的后续 schedule 任务。
 *
 * Runtime Data (Read-only)
 *   - StoryInstance : Ink.Runtime.Story
 *   - CurrentInkId  : string   // 当前归属 InkId（用于 schedule 默认归属与跨文件安全回跳）
 *
 * Events (UI should subscribe)
 *   - OnLine(string line)                 // 输出一行普通文本
 *   - OnChoices(List<Choice> choices)     // 输出当前选项列表（可能为空）
 *   - OnStoryEnded()                      // 故事在当前点结束（无 canContinue 且无 choices）
 *
 * Ink Command Protocol (via tags)
 *   Tag prefix: "#cmd:" (configurable via commandTagPrefix)
 *
 *   - ps_event <PersonalityEventTag> <impact>
 *   - hs_event <HealthEventType> <amount>
 *   - hs_sleep <true/false>
 *   - hs_activity <ActivityLevel>
 *   - schedule <knot> <delay> [planId] [cancelOnReply]
 *       默认归属 currentInkId（避免切换 Ink 后到点找不到 knot）
 *   - schedule_to <inkId> <knot> <delay> [planId] [cancelOnReply]
 *       指定归属 inkId（推荐做法）
 *   - switch_ink <inkId> [startKnot]
 *   - plan_arm <planId>
 *
 * Time & Scheduling Notes
 *   - 调度使用 TimeManager.GameTimeMinutes 的“总分钟轴”（通过 GameTickEvent.TotalMinutes 驱动检查）。
 *   - 到点触发时先切回归属 Ink（currentInkId / 指定 inkId），再进入 knot，避免跨文件报错。
 *   - 同一帧只触发一个 pending trigger，避免刷屏。
 */

using System;
using System.Collections;
using System.Collections.Generic;
using Ink.Runtime;
using UnityEngine;
using ProjectSulamith.Core;

namespace ProjectSulamith.Dialogue
{
    /// <summary>
    /// Ink 管理器（命令驱动版）：
    /// - Ink 只输出文本与 tags
    /// - InkManager 监测 tags 中的命令并执行（人格/健康/时间调度/切换故事）
    ///
    /// 支持命令：
    ///   - ps_event <PersonalityEventTag> <impact>
    ///   - hs_event <HealthEventType> <amount>
    ///   - hs_sleep <true/false>
    ///   - hs_activity <ActivityLevel>
    ///   - schedule <knot> <delay> [planId] [cancelOnReply]          // 默认归属当前 Ink 文件
    ///   - schedule_to <inkId> <knot> <delay> [planId] [cancelOnReply] // 指定归属 Ink 文件（推荐）
    ///   - switch_ink <inkId> [startKnot]
    ///   - plan_arm <planId>                                        // 装填“可被玩家回复打断”的计划流
    ///
    /// 说明：
    /// - “时间调度”不再只存 knot，而是存 (inkId, knot)，到点会自动切回对应 ink 再进入 knot，避免切走文件时报错。
    /// - “计划消息流”用于复刻《生命线》风格：计划发多段文字，玩家在第一段时回复会取消后续段（cancelOnReply=true）。
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

        private readonly Queue<string> _externalCommandQueue = new Queue<string>();//外部命令接口

        // =====================================================================
        // Ink Identity（不改你原有接口，仅补强：用于 schedule 默认归属）
        // =====================================================================
        [Header("Ink Identity")]
        [Tooltip("当前运行中的 InkId（用于 schedule 默认归属）。建议主文件填 MainTest / MainChat 等。")]
        [SerializeField] private string currentInkId = "Main";
        public string CurrentInkId => currentInkId;

        // =====================================================================
        // 《生命线》式“计划消息流”：玩家回复时可取消后续计划消息
        // =====================================================================
        // 当前“已装填”的计划 ID：当玩家 ChooseOption 时，会取消该计划中 CancelOnReply=true 的任务
        private string _armedPlanId = null;

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

            // 注意：这里不强制修改 currentInkId（保持 Inspector 配置为主 InkId）
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

            // ★ 玩家回复 = “打断点”：若装填了计划流，则取消该计划后续消息（CancelOnReply=true 的任务）
            if (!string.IsNullOrEmpty(_armedPlanId))
            {
                CancelPlanScheduledTasks(_armedPlanId);
                _armedPlanId = null;
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
        private bool ExecuteCommandPayload(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload)) return false;

            if (logCommands) Debug.Log($"[InkCmd] {payload}");

            var parts = payload.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return false;

            string cmd = parts[0];

            try
            {
                switch (cmd)
                {
                    case "ps_event":
                        HandlePsEvent(parts);
                        return true;

                    case "hs_event":
                        HandleHsEvent(parts);
                        return true;

                    case "hs_sleep":
                        HandleHsSleep(parts);
                        return true;

                    case "hs_activity":
                        HandleHsActivity(parts);
                        return true;

                    case "schedule":
                        HandleSchedule(parts);
                        return true;

                    case "schedule_to":
                        HandleScheduleTo(parts);
                        return true;

                    case "switch_ink":
                        HandleSwitchInk(parts);
                        return true;

                    case "plan_arm":
                        HandlePlanArm(parts);
                        return true;

                    default:
                        if (warnUnknownCommands)
                            Debug.LogWarning($"[InkCmd] Unknown command: {cmd} (raw='{payload}')");
                        return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InkCmd] Execute failed: '{payload}'  ex={ex}");
                return false;
            }
        }
        //方法

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

                // 执行：统一走 ExecuteCommandPayload
                ExecuteCommandPayload(payload);
            }
        }
        public bool ExecuteCommand(string payload)
        {
            return ExecuteCommandPayload(payload);
        }

        //外部方法走这里

        // =====================================================================
        // schedule / schedule_to / plan_arm
        // =====================================================================

        private void HandleSchedule(string[] parts)
        {
            // schedule <knot> <delay> [planId] [cancelOnReply]
            if (parts.Length < 3)
            {
                Debug.LogWarning("[InkCmd] schedule requires: schedule <KnotName> <Delay e.g. 10m/3h/1d> [planId] [cancelOnReply]");
                return;
            }

            string knot = parts[1];
            string delayRaw = parts[2];

            if (!TryParseDelayToMinutes(delayRaw, out float minutes))
            {
                Debug.LogWarning("[InkCmd] Invalid delay format: " + delayRaw);
                return;
            }

            string planId = parts.Length >= 4 ? parts[3] : null;
            bool cancelOnReply = false;
            if (parts.Length >= 5) bool.TryParse(parts[4], out cancelOnReply);

            // ★ 关键：默认归属当前 Ink（currentInkId），避免切走文件导致到点找不到 knot
            ScheduleKnot(currentInkId, knot, minutes, planId, cancelOnReply);
        }

        private void HandleScheduleTo(string[] parts)
        {
            // schedule_to <InkId> <Knot> <Delay> [planId] [cancelOnReply]
            if (parts.Length < 4)
            {
                Debug.LogWarning("[InkCmd] schedule_to requires: schedule_to <InkId> <Knot> <Delay e.g. 10m/3h/1d> [planId] [cancelOnReply]");
                return;
            }

            string inkId = parts[1];
            string knot = parts[2];
            string delayRaw = parts[3];

            if (!TryParseDelayToMinutes(delayRaw, out float minutes))
            {
                Debug.LogWarning("[InkCmd] Invalid delay format: " + delayRaw);
                return;
            }

            string planId = parts.Length >= 5 ? parts[4] : null;
            bool cancelOnReply = false;
            if (parts.Length >= 6) bool.TryParse(parts[5], out cancelOnReply);

            ScheduleKnot(inkId, knot, minutes, planId, cancelOnReply);
        }

        private void HandlePlanArm(string[] parts)
        {
            // plan_arm <planId>
            if (parts.Length < 2)
            {
                Debug.LogWarning("[InkCmd] plan_arm requires: plan_arm <PlanId>");
                return;
            }

            _armedPlanId = parts[1];
            if (logCommands) Debug.Log($"[InkPlan] Armed plan '{_armedPlanId}'");
        }

        private bool TryParseDelayToMinutes(string raw, out float minutes)
        {
            minutes = 0f;
            if (string.IsNullOrWhiteSpace(raw)) return false;

            raw = raw.Trim().ToLowerInvariant();

            // 支持：纯数字 = 分钟；10m/3h/1d
            char last = raw[raw.Length - 1];
            string numPart = raw;
            float multiplier = 1f;

            if (char.IsLetter(last))
            {
                numPart = raw.Substring(0, raw.Length - 1);

                switch (last)
                {
                    case 'm': multiplier = 1f; break;
                    case 'h': multiplier = 60f; break;
                    case 'd': multiplier = 1440f; break;
                    case 's': multiplier = 1f / 60f; break; // ★ 额外支持秒（测试用更方便）：10s
                    default: return false;
                }
            }

            if (!float.TryParse(numPart, out float n)) return false;
            if (n < 0f) return false;

            minutes = n * multiplier;
            return true;
        }

        // =====================================================================
        // 人格 / 健康命令（保留你原逻辑）
        // =====================================================================

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

            var e = new PersonalitySystem.PersonalityEvent
            {
                Tag = tag,
                Impact = Mathf.Clamp01(impact <= 0 ? 0.7f : impact),

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

        // =====================================================================
        // 时间调度：升级为 (inkId, knot) + 计划流取消
        // =====================================================================

        [Serializable]
        private class ScheduledInkTask
        {
            public string Id;
            public double DueTotalMinutes;   // 使用 TimeManager.GameTimeMinutes 的“总分钟轴”
            public string InkId;            // ★ 归属 Ink（到点先切回该 Ink）
            public string Knot;             // 目标 knot

            // ★ 《生命线》式计划消息流：
            // PlanId：同一个计划中的多段消息共享同一 PlanId
            // CancelOnReply：若玩家在第一段处回复，则取消计划中这些剩余段
            public string PlanId;
            public bool CancelOnReply;
        }

        // 当前所有待触发任务
        [SerializeField] private List<ScheduledInkTask> _scheduledTasks = new List<ScheduledInkTask>();

        // 触发队列：不再只存 knot，而是存 (InkId, Knot)，确保跨文件安全
        private struct InkTrigger
        {
            public string InkId;
            public string Knot;
        }

        private readonly Queue<InkTrigger> _pendingKnotTriggers = new Queue<InkTrigger>();

        private EventBus _bus;

        private void OnEnable()
        {
            _bus = EventBus.Instance;
            _bus?.Subscribe<GameTickEvent>(OnGameTick);
        }

        private void OnDisable()
        {
            _bus?.Unsubscribe<GameTickEvent>(OnGameTick);
            _bus = null;
        }

        private void OnGameTick(GameTickEvent e)
        {
            CheckScheduledTasks(e.TotalMinutes);
        }

        private double GetNowTotalMinutes()
        {
            return TimeManager.Instance != null ? TimeManager.Instance.GameTimeMinutes : 0.0;

        }

        // ★ 核心：调度任务携带 inkId（归属）、可选 planId、可选 cancelOnReply
        private void ScheduleKnot(string inkId, string knot, float delayMinutes, string planId = null, bool cancelOnReply = false)
        {
            if (string.IsNullOrWhiteSpace(inkId)) inkId = currentInkId;
            if (string.IsNullOrWhiteSpace(knot)) return;

            double now = GetNowTotalMinutes();
            double due = now + Mathf.Max(0f, delayMinutes);

            _scheduledTasks.Add(new ScheduledInkTask
            {
                Id = Guid.NewGuid().ToString("N"),
                DueTotalMinutes = due,
                InkId = inkId,
                Knot = knot,
                PlanId = planId,
                CancelOnReply = cancelOnReply
            });

            Debug.Log($"[InkSchedule] ink='{inkId}' knot='{knot}' due={due:F2} (in {delayMinutes:F2}m) plan='{planId}' cancelOnReply={cancelOnReply}");
        }

        private void CheckScheduledTasks(double nowTotalMinutes)
        {
            if (_scheduledTasks == null || _scheduledTasks.Count == 0) return;

            for (int i = _scheduledTasks.Count - 1; i >= 0; i--)
            {
                var t = _scheduledTasks[i];
                if (t != null && t.DueTotalMinutes <= nowTotalMinutes)
                {
                    _scheduledTasks.RemoveAt(i);

                    // 不在 Tick 回调里直接 StartStory；先入队，下一帧执行
                    _pendingKnotTriggers.Enqueue(new InkTrigger { InkId = t.InkId, Knot = t.Knot });
                }
            }
        }

        // ★ 计划流取消：删除该计划中 CancelOnReply=true 的剩余任务
        private void CancelPlanScheduledTasks(string planId)
        {
            if (string.IsNullOrEmpty(planId)) return;
            if (_scheduledTasks == null || _scheduledTasks.Count == 0) return;

            int before = _scheduledTasks.Count;
            _scheduledTasks.RemoveAll(t =>
                t != null &&
                string.Equals(t.PlanId, planId, StringComparison.Ordinal) &&
                t.CancelOnReply);

            int removed = before - _scheduledTasks.Count;
            Debug.Log($"[InkPlan] Cancelled plan='{planId}' removed={removed}");
        }

        private void Update()
        {
            // 一帧只触发一个，避免到点任务太多刷屏
            if (_pendingKnotTriggers.Count > 0)
            {
                var trig = _pendingKnotTriggers.Dequeue();

                // 若 ContinueStory 正在跑，延迟一帧触发（更稳）
                if (_isContinuing)
                {
                    _pendingKnotTriggers.Enqueue(trig);
                    return;
                }

                // ★ 到点先切回归属 Ink，再进入 knot（彻底解决“切走文件到点报错”）
                if (!string.Equals(trig.InkId, currentInkId, StringComparison.Ordinal))
                {
                    SwitchInk(trig.InkId, trig.Knot);
                }
                else
                {
                    StartStory(trig.Knot);
                }
            }
        }

        // =====================================================================
        // Ink Library + 切换 Ink（保留你原接口，只补强 currentInkId 的维护）
        // =====================================================================

        [Serializable]
        public class InkStoryEntry
        {
            public string Id;          // 逻辑名，例如 "TechDiscussion"
            public TextAsset InkJson;  // 对应的 ink JSON
        }

        [Header("Ink Library")]
        public List<InkStoryEntry> inkLibrary = new List<InkStoryEntry>();

        // 切换 ink 文件
        private void SwitchInk(string inkId, string startKnot = null)
        {
            var entry = inkLibrary.Find(e => e.Id == inkId);
            if (entry == null || entry.InkJson == null)
            {
                Debug.LogWarning($"[InkManager] Ink '{inkId}' not found in library.");
                return;
            }

            // ★ 维护当前 InkId：后续 schedule 默认归属将以此为准
            currentInkId = inkId;

            // 中断当前故事（可选：通知 UI 清空）
            StoryInstance = new Story(entry.InkJson.text);

            if (!string.IsNullOrEmpty(startKnot))
                StoryInstance.ChoosePathString(startKnot);

            Debug.Log($"[InkManager] Switched Ink to '{inkId}', start='{startKnot}'");

            ContinueStory();
        }

        private void HandleSwitchInk(string[] parts)
        {
            // switch_ink <InkId> [StartKnot]
            if (parts.Length < 2)
            {
                Debug.LogWarning("[InkCmd] switch_ink requires: switch_ink <InkId> [StartKnot]");
                return;
            }

            string inkId = parts[1];
            string knot = parts.Length >= 3 ? parts[2] : null;

            // 为了安全：不要在 ContinueStory 循环中直接切
            StartCoroutine(SwitchInkNextFrame(inkId, knot));
        }

        private IEnumerator SwitchInkNextFrame(string inkId, string knot)
        {
            yield return null;
            SwitchInk(inkId, knot);
        }
    }
    

    }
