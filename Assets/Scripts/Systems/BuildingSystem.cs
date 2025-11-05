// Assets/Scripts/Systems/BuildingSystem.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using ProjectSulamith.Core;

namespace ProjectSulamith.Systems
{
    /// <summary>
    /// 最小建造流程（三资源版）：
    /// 收到 BuildRequest → 发起 SpendResourcesRequest(三资源) → 等待 SpendResourcesResult →
    /// 成功则发布 BuildAccepted，失败则 BuildRejected。
    /// </summary>
    public class BuildingSystem : MonoBehaviour, ISimSystem
    {
        // 记录“待扣费”的建造请求：TxId -> PrototypeId
        private readonly Dictionary<Guid, string> _pending = new Dictionary<Guid, string>();

        public void Initialize() { }
        public void Shutdown() { }
        public void Tick(float dm) { }

        private void OnEnable()
        {
            EventBus.Instance?.Subscribe<BuildRequest>(OnBuildRequest);
            EventBus.Instance?.Subscribe<SpendResourcesResult>(OnSpendResourcesResult);
        }

        private void OnDisable()
        {
            if (EventBus.Instance == null) return;
            EventBus.Instance.Unsubscribe<BuildRequest>(OnBuildRequest);
            EventBus.Instance.Unsubscribe<SpendResourcesResult>(OnSpendResourcesResult);
        }

        private void OnBuildRequest(BuildRequest req)
        {
            // 记录挂起事务
            _pending[req.TxId] = req.PrototypeId;

            // 发起三资源扣费请求（仅整数）
            EventBus.Instance?.Publish(new SpendResourcesRequest
            {
                Food = Mathf.Max(0, req.FoodCost),
                Mat = Mathf.Max(0, req.MatCost),
                Energy = Mathf.Max(0, req.EnergyCost),
                TxId = req.TxId
            });
        }

        private void OnSpendResourcesResult(SpendResourcesResult res)
        {
            // 只处理自己记录的事务
            if (!_pending.TryGetValue(res.TxId, out var protoId))
                return;

            if (res.Ok)
            {
                // 扣费成功 → 开始建造（此处仅发布通过事件，具体入队/计时/生成由上层实现）
                EventBus.Instance?.Publish(new BuildAccepted
                {
                    PrototypeId = protoId,
                    TxId = res.TxId
                });

                // TODO: 如果需要：将 protoId 入建造队列，启动计时，广播 BuildQueued/BuildStarted/BuildProgress...
            }
            else
            {
                // 扣费失败
                EventBus.Instance?.Publish(new BuildRejected
                {
                    PrototypeId = protoId,
                    Reason = "Not enough resources", // 如需更细原因，可在 ResourceSystem 里带上失败码
                    TxId = res.TxId
                });
            }

            // 清理挂起记录
            _pending.Remove(res.TxId);
        }
    }
}
