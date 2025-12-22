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
        private readonly Dictionary<Guid, BuildRequest> _pending = new Dictionary<Guid, BuildRequest>();


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
        //启用和禁用


        private void OnBuildRequest(BuildRequest req)
        {
            _pending[req.TxId] = req;

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
            if (!_pending.TryGetValue(res.TxId, out var req))
                return;

            if (res.Ok)
            {
                EventBus.Instance?.Publish(new BuildAccepted
                {
                    PrototypeId = req.PrototypeId,
                    CellPosition = req.CellPosition,
                    TxId = res.TxId
                });
            }
            else
            {
                EventBus.Instance?.Publish(new BuildRejected
                {
                    PrototypeId = req.PrototypeId,
                    CellPosition = req.CellPosition,
                    Reason = "Not enough resources",
                    TxId = res.TxId
                });
            }

            _pending.Remove(res.TxId);
        }

    }
}
