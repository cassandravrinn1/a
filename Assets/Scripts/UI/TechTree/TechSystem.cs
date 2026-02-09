using System;
using System.Collections.Generic;
using UnityEngine;
using ProjectSulamith.Dialogue;

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

    /// <summary>
    /// TechSystem（一体化 v1）：
    /// - 状态表：Locked/Available/Discussing/Unlocked/Rejected/Deferred
    /// - 前置判断：TechNodeData.prerequisites
    /// - 讨论入口：StartDiscussion(node) -> inkManager.ExecuteCommand("switch_ink ...")
    /// - Ink 回写：OnTechCommit(techId, decision) 由 InkManager 在解析到 tech_commit 时调用
    ///
    /// v1 策略（最简闭环）：
    /// - 前置不满足：默认不允许讨论（可用 allowDiscussWhenLocked 开关放开）
    /// - Unlocked 后调用 RefreshAvailability 推动后续可用
    /// </summary>
    public class TechSystem : MonoBehaviour
    {
        #region Singleton

        public static TechSystem Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            BuildIndex();
            InitializeStates();
            RefreshAvailability(notify: false);
        }

        #endregion

        #region Inspector

        [Header("Tech Nodes")]
        [Tooltip("全量科技节点（用于初始化/刷新可用性）。通常由 TechTree UI 生成器持有同一份列表。")]
        public List<TechNodeData> allNodes = new List<TechNodeData>();

        [Header("Ink")]
        [Tooltip("场景中的 InkManager 引用（必须填写）。")]
        public InkManager inkManager;

        [Header("Policy")]
        [Tooltip("Rejected 后是否允许再次讨论（默认 false）")]
        public bool allowReDiscussRejected = false;

        [Tooltip("Deferred 后是否允许再次讨论（默认 true）")]
        public bool allowReDiscussDeferred = true;

        [Header("Debug")]
        public bool logTech = false;

        #endregion

        #region Public Events

        /// <summary>
        /// 给 UI 刷新用：techId,newState
        /// </summary>
        public event Action<string, TechState> OnStateChanged;

        #endregion

        #region Runtime

        private readonly Dictionary<string, TechNodeData> _nodeById = new Dictionary<string, TechNodeData>();
        private readonly Dictionary<string, TechState> _stateById = new Dictionary<string, TechState>();

        public bool IsDiscussing { get; private set; }
        public string CurrentTechId { get; private set; }

        #endregion

        #region Index / Init
        public void RebuildFromNodes(List<TechNodeData> nodes, bool refreshAvailability = true, bool notify = false)
        {
            allNodes = nodes ?? new List<TechNodeData>();

            // 重新建立索引
            _nodeById.Clear();
            foreach (var n in allNodes)
            {
                if (n == null) continue;
                if (string.IsNullOrEmpty(n.id))
                {
                    Debug.LogWarning("[TechSystem] TechNodeData has empty id, skipped.");
                    continue;
                }

                if (_nodeById.ContainsKey(n.id))
                    Debug.LogWarning($"[TechSystem] Duplicate tech id '{n.id}', last one wins.");

                _nodeById[n.id] = n;

                // 初始化尚未出现过的状态 key（给存档/运行期扩展留余地）
                if (!_stateById.ContainsKey(n.id))
                    _stateById[n.id] = TechState.Locked;
            }

            

            if (refreshAvailability)
                RefreshAvailability(notify);
        }

        private void BuildIndex()
        {
            _nodeById.Clear();

            foreach (var n in allNodes)
            {
                if (n == null) continue;
                if (string.IsNullOrEmpty(n.id))
                {
                    Debug.LogWarning("[TechSystem] TechNodeData has empty id, skipped.");
                    continue;
                }

                if (_nodeById.ContainsKey(n.id))
                    Debug.LogWarning($"[TechSystem] Duplicate tech id '{n.id}', last one wins.");

                _nodeById[n.id] = n;
            }
        }

        private void InitializeStates()
        {
            foreach (var kv in _nodeById)
            {
                if (!_stateById.ContainsKey(kv.Key))
                    _stateById[kv.Key] = TechState.Locked;
            }
        }

        #endregion

        #region Query

        public TechNodeData GetNode(string techId)
        {
            if (string.IsNullOrEmpty(techId)) return null;
            _nodeById.TryGetValue(techId, out var n);
            return n;
        }

        public TechState GetState(string techId)
        {
            if (string.IsNullOrEmpty(techId)) return TechState.Locked;
            return _stateById.TryGetValue(techId, out var s) ? s : TechState.Locked;
        }

        public bool IsUnlocked(string techId) => GetState(techId) == TechState.Unlocked;

        public bool PrerequisitesMet(TechNodeData node)
        {
            if (node == null) return false;

            var prereqs = node.prerequisites;
            if (prereqs == null || prereqs.Count == 0) return true;

            for (int i = 0; i < prereqs.Count; i++)
            {
                var pre = prereqs[i];
                if (pre == null) continue;
                if (string.IsNullOrEmpty(pre.id)) continue;

                if (!IsUnlocked(pre.id))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// v1：是否允许开启讨论（不考虑研究点/资源/耗时，只看状态与前置）
        /// </summary>
        public bool CanDiscuss(TechNodeData node)
        {
            if (node == null) return false;
            if (IsDiscussing) return false;

            var st = GetState(node.id);

            if (st == TechState.Unlocked) return false;
            if (st == TechState.Rejected && !allowReDiscussRejected) return false;
            if (st == TechState.Deferred && !allowReDiscussDeferred) return false;

            // 前置未满足：默认不允许；如果你在 TechNodeData 里加了 allowDiscussWhenLocked，则允许“理论讨论”
            if (!PrerequisitesMet(node))
            {
                // 如果你没加该字段，把这句改成 return false;
                return node.allowDiscussWhenLocked;
            }

            // 必须配置讨论 Ink
            if (string.IsNullOrEmpty(node.discussionInkId)) return false;

            return true;
        }

        #endregion

        #region State

        public void SetState(string techId, TechState newState, bool notify = true)
        {
            if (string.IsNullOrEmpty(techId)) return;

            _stateById[techId] = newState;

            if (logTech) Debug.Log($"[TechSystem] {techId} -> {newState}");

            if (notify)
                OnStateChanged?.Invoke(techId, newState);
        }

        /// <summary>
        /// 刷新 Locked/Available（不会覆盖 Unlocked/Rejected/Discussing）
        /// </summary>
        public void RefreshAvailability(bool notify = true)
        {
            foreach (var kv in _nodeById)
            {
                var id = kv.Key;
                var node = kv.Value;

                var st = GetState(id);
                if (st == TechState.Unlocked || st == TechState.Rejected || st == TechState.Discussing)
                    continue;

                var target = PrerequisitesMet(node) ? TechState.Available : TechState.Locked;
                if (target != st)
                    SetState(id, target, notify);
            }
        }

        #endregion

        #region Discussion Flow

        /// <summary>
        /// UI 节点按钮调用：开启讨论（会调用 InkManager.ExecuteCommand -> switch_ink）
        /// </summary>
        public bool StartDiscussion(TechNodeData node)
        {
            if (node == null) return false;

            if (inkManager == null)
            {
                Debug.LogError("[TechSystem] inkManager is null. Assign it in Inspector.");
                return false;
            }

            if (!CanDiscuss(node)) return false;

            IsDiscussing = true;
            CurrentTechId = node.id;

            SetState(node.id, TechState.Discussing, notify: true);

            string entry = string.IsNullOrEmpty(node.discussionEntryKnot) ? "start" : node.discussionEntryKnot;

            // 关键：直接走你的外部接口（同一条命令管线）
            inkManager.ExecuteCommand($"switch_ink {node.discussionInkId} {entry}");

            return true;
        }

        private void EndDiscussionInternal()
        {
            IsDiscussing = false;
            CurrentTechId = null;
        }

        /// <summary>
        /// 由 InkManager 在解析到 "tech_commit" 命令时调用：
        /// Ink 写法：#cmd: tech_commit <techId> <unlock|reject|defer>
        /// </summary>
        public void OnTechCommit(string techId, string decision)
        {
            if (string.IsNullOrEmpty(techId))
            {
                Debug.LogWarning("[TechSystem] tech_commit missing techId.");
                return;
            }

            TechState target;

            switch (decision)
            {
                case "unlock":
                    target = TechState.Unlocked;
                    break;

                case "reject":
                    target = TechState.Rejected;
                    break;

                case "defer":
                    target = TechState.Deferred;
                    break;

                default:
                    Debug.LogWarning($"[TechSystem] tech_commit unknown decision '{decision}' (techId={techId})");
                    var node = GetNode(techId);
                    target = (node != null && PrerequisitesMet(node)) ? TechState.Available : TechState.Locked;
                    break;
            }

            SetState(techId, target, notify: true);

            EndDiscussionInternal();
            RefreshAvailability(notify: true);
        }

        #endregion

        #region Optional Save/Load Helpers

        public Dictionary<string, TechState> ExportStates()
            => new Dictionary<string, TechState>(_stateById);

        public void ApplySavedStates(Dictionary<string, TechState> saved, bool notify = false)
        {
            if (saved == null) return;

            foreach (var kv in saved)
                _stateById[kv.Key] = kv.Value;

            InitializeStates();
            RefreshAvailability(notify);
        }

        #endregion
    }
}
