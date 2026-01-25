using System.Collections.Generic;
using UnityEngine;

namespace ProjectSulamith.TechTree
{
    public enum TechState
    {
        Locked,
        Available,
        Discussing,
        Unlocked,
        Rejected,
        Deferred
    }

    public class TechSystem : MonoBehaviour
    {
        public static TechSystem Instance { get; private set; }

        [Header("All Tech Nodes (for init & refresh)")]
        public List<TechNodeData> allNodes = new List<TechNodeData>();

        [Header("Ink Command Entry")]
        [Tooltip("你的 InkManager（需要提供 ExecuteCommand(string) 外部接口）")]
        public MonoBehaviour inkManagerBehaviour; // 拖拽你的 InkManager
        private IInkCommandSink _ink;

        // 状态表
        private readonly Dictionary<string, TechState> _states = new();

        // 讨论会话互斥
        public bool IsDiscussing { get; private set; }
        public string CurrentTechId { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _ink = inkManagerBehaviour as IInkCommandSink;
            if (_ink == null && inkManagerBehaviour != null)
                Debug.LogError("[TechSystem] inkManagerBehaviour must implement IInkCommandSink (ExecuteCommand).");

            InitializeStates();
            RefreshAvailability();
        }

        private void InitializeStates()
        {
            _states.Clear();

            foreach (var n in allNodes)
            {
                if (n == null || string.IsNullOrEmpty(n.id)) continue;
                _states[n.id] = TechState.Locked;
            }
        }

        // =========================
        // Queries
        // =========================

        public TechState GetState(string techId)
            => _states.TryGetValue(techId, out var s) ? s : TechState.Locked;

        public bool IsUnlocked(string techId) => GetState(techId) == TechState.Unlocked;

        public bool PrerequisitesMet(TechNodeData node)
        {
            if (node == null) return false;
            if (node.prerequisites == null || node.prerequisites.Count == 0) return true;

            foreach (var pre in node.prerequisites)
            {
                if (pre == null) continue;
                if (!IsUnlocked(pre.id)) return false;
            }
            return true;
        }

        public bool CanDiscuss(TechNodeData node)
        {
            if (node == null) return false;

            var st = GetState(node.id);
            if (st == TechState.Unlocked) return false; // 已解锁无需讨论

            if (PrerequisitesMet(node)) return true;
            return node.allowDiscussWhenLocked;
        }

        // =========================
        // State mutations
        // =========================

        public void SetState(string techId, TechState state)
        {
            if (string.IsNullOrEmpty(techId)) return;
            _states[techId] = state;
        }

        public void RefreshAvailability()
        {
            foreach (var n in allNodes)
            {
                if (n == null || string.IsNullOrEmpty(n.id)) continue;

                var st = GetState(n.id);
                if (st == TechState.Unlocked || st == TechState.Rejected) continue;
                if (st == TechState.Discussing) continue; // 讨论中别被刷新覆盖

                _states[n.id] = PrerequisitesMet(n) ? TechState.Available : TechState.Locked;
            }
        }

        // =========================
        // Tech discussion entry
        // =========================

        public bool StartDiscussion(TechNodeData node)
        {
            if (node == null) return false;
            if (IsDiscussing) return false;
            if (!CanDiscuss(node)) return false;

            if (string.IsNullOrEmpty(node.discussionInkId))
            {
                Debug.LogError($"[TechSystem] Tech '{node.id}' missing discussionInkId.");
                return false;
            }

            IsDiscussing = true;
            CurrentTechId = node.id;

            SetState(node.id, TechState.Discussing);

            // 统一从这里走你现有 cmd 管线
            if (_ink == null)
            {
                Debug.LogError("[TechSystem] Ink command sink not set.");
                return false;
            }

            _ink.ExecuteCommand($"switch_ink {node.discussionInkId} {node.discussionEntryKnot}");

            return true;
        }

        private void EndDiscussionInternal()
        {
            IsDiscussing = false;
            CurrentTechId = null;
        }

        // =========================
        // Called by InkManager when cmd is tech_commit
        // =========================

        /// <summary>
        /// Ink: #cmd: tech_commit <techId> <unlock|reject|defer>
        /// </summary>
        public void OnTechCommit(string techId, string decision)
        {
            if (string.IsNullOrEmpty(techId))
            {
                Debug.LogWarning("[TechSystem] OnTechCommit techId is empty.");
                return;
            }

            switch (decision)
            {
                case "unlock":
                    SetState(techId, TechState.Unlocked);
                    break;

                case "reject":
                    SetState(techId, TechState.Rejected);
                    break;

                case "defer":
                    SetState(techId, TechState.Deferred);
                    break;

                default:
                    Debug.LogWarning($"[TechSystem] Unknown decision '{decision}' for tech '{techId}'.");
                    // 回退为 Available/Locked 取决于前置
                    SetState(techId, PrerequisitesMet(FindNode(techId)) ? TechState.Available : TechState.Locked);
                    break;
            }

            EndDiscussionInternal();
            RefreshAvailability();

            // 这里你可以通知 UI 刷新（如果 UI 不是每帧轮询）
            // EventBus.Instance?.Publish(new TechStateChangedEvent { TechId = techId });
        }

        private TechNodeData FindNode(string techId)
        {
            if (string.IsNullOrEmpty(techId)) return null;
            foreach (var n in allNodes)
                if (n != null && n.id == techId) return n;
            return null;
        }
    }

    /// <summary>
    /// 让你的 InkManager 实现这个接口即可被 TechSystem 调用 ExecuteCommand。
    /// </summary>
    public interface IInkCommandSink
    {
        void ExecuteCommand(string cmdLine);
    }
}
