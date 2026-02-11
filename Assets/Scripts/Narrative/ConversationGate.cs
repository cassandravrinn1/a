using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectSulamith.Dialogue
{
    /// <summary>
    /// 超极简会话仲裁器（无分类 / 无合并）
    /// - 状态：Idle / Busy / Sleeping
    /// - 只看 priority 与 forceInterrupt
    /// - Sleeping/Busy 时默认入队；Idle 时执行
    /// - Idle 时吐队列：最高 priority，平级按先来先服务
    /// </summary>
    public class ConversationGate : MonoBehaviour
    {
        public enum SessionState
        {
            Idle,       // 空闲窗口：允许开启新话题
            Busy,       // 正在对话中（输出/等待玩家）
            Sleeping    // 睡觉：不回应，话题累积
        }

        [Serializable]
        public struct Request
        {
            public string InkId;
            public string Knot;
            public int Priority;            // 越大越优先
            public bool ForceInterrupt;     // 强制打断
            public double CreatedAt;        // 用于平级 FIFO
        }

        [Header("Refs")]
        public InkManager inkManager;

        [Header("Policy")]
        [Tooltip("Busy 状态下是否允许 forceInterrupt 立刻抢占")]
        public bool allowForceDuringBusy = true;

        [Tooltip("Sleeping 状态下是否允许 forceInterrupt 立刻抢占（一般用于紧急事件）")]
        public bool allowForceDuringSleeping = true;

        [Header("Debug")]
        public bool logGate = false;

        public SessionState State { get; private set; } = SessionState.Idle;
        public bool IsSleeping { get; private set; } = false;

        private readonly List<Request> _queue = new List<Request>();

        private void Awake()
        {
            if (inkManager == null) inkManager = GetComponent<InkManager>();
            if (inkManager == null) Debug.LogError("[UltraGate] Missing InkManager reference.");
        }

        private void Update()
        {
            UpdateState();
            DrainIfIdle();
        }

        /// <summary>
        /// 由 InkManager 在 hs_sleep 后同步调用
        /// </summary>
        public void SetSleeping(bool sleeping)
        {
            IsSleeping = sleeping;
            if (logGate) Debug.Log($"[UltraGate] Sleeping={IsSleeping}");
            UpdateState();

            if (!IsSleeping)
                DrainIfIdle(); // 刚醒来，尝试立即处理
        }

        /// <summary>
        /// 统一入口：请求开启某个 InkId/Knot
        /// </summary>
        public void RequestSwitch(string inkId, string knot, int priority, bool forceInterrupt = false)
        {
            if (inkManager == null) return;
            if (string.IsNullOrWhiteSpace(inkId)) return;
            if (string.IsNullOrWhiteSpace(knot)) knot = "start";

            var req = new Request
            {
                InkId = inkId,
                Knot = knot,
                Priority = priority,
                ForceInterrupt = forceInterrupt,
                CreatedAt = Time.unscaledTimeAsDouble
            };

            if (ShouldExecuteImmediately(req))
            {
                Execute(req);
            }
            else
            {
                _queue.Add(req);
                if (logGate) Debug.Log($"[UltraGate] Enqueued prio={priority} force={forceInterrupt} -> {inkId}:{knot}");
            }
        }

        private bool ShouldExecuteImmediately(Request req)
        {
            if (State == SessionState.Sleeping)
            {
                if (!allowForceDuringSleeping) return false;
                return req.ForceInterrupt;
            }

            if (State == SessionState.Busy)
            {
                if (!allowForceDuringBusy) return false;
                return req.ForceInterrupt;
            }

            // Idle
            return true;
        }

        private void DrainIfIdle()
        {
            if (State != SessionState.Idle) return;
            if (_queue.Count == 0) return;

            int bestIndex = -1;
            int bestPrio = int.MinValue;
            double bestTime = double.MaxValue;

            for (int i = 0; i < _queue.Count; i++)
            {
                var r = _queue[i];
                if (r.Priority > bestPrio || (r.Priority == bestPrio && r.CreatedAt < bestTime))
                {
                    bestIndex = i;
                    bestPrio = r.Priority;
                    bestTime = r.CreatedAt;
                }
            }

            if (bestIndex < 0) return;

            var req = _queue[bestIndex];
            _queue.RemoveAt(bestIndex);

            if (logGate) Debug.Log($"[UltraGate] Dequeued prio={req.Priority} -> {req.InkId}:{req.Knot}");
            Execute(req);
        }

        private void Execute(Request req)
        {
            if (inkManager == null) return;

            // 统一走 cmd：保持你 InkManager 的协议一致
            inkManager.MarkScheduledInsertBegin();

            inkManager.ExecuteCommand($"switch_ink {req.InkId} {req.Knot}");
        }

        private void UpdateState()
        {
            if (IsSleeping)
            {
                State = SessionState.Sleeping;
                return;
            }

            if (inkManager == null || inkManager.StoryInstance == null)
            {
                State = SessionState.Idle;
                return;
            }

            // 保守判定：只要能继续输出 或 有选项等待玩家，就视为 Busy
            bool canContinue = inkManager.StoryInstance.canContinue;
            bool hasChoices = inkManager.StoryInstance.currentChoices != null && inkManager.StoryInstance.currentChoices.Count > 0;

            State = (canContinue || hasChoices) ? SessionState.Busy : SessionState.Idle;
        }
    }
}
