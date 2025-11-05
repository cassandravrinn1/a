// Assets/Scripts/Systems/ResourceSystem.cs
using UnityEngine;
using ProjectSulamith.Core;
using System;

namespace ProjectSulamith.Systems
{
    /// <summary>
    /// 资源系统（权威）：库存使用整数；小数用于内部累加，凑整后再入库。
    /// 一切消费仅使用“整数库存”，保证“不会超过整数部分”。
    /// </summary>
    public class ResourceSystem : MonoBehaviour, ISimSystem
    {
        [Header("Caps（整数上限）")]
        [SerializeField] private int foodCap = 1000;
        [SerializeField] private int matCap = 1000;
        [SerializeField] private int energyCap = 1000;

        [Header("Produce per minute（可小数）")]
        [SerializeField] private float foodProducePerMin = 5f;
        [SerializeField] private float matProducePerMin = 5f;
        [SerializeField] private float energyProducePerMin = 5f;

        [Header("Consume per minute（可小数）")]
        [SerializeField] private float foodConsumePerMin = 2f;
        [SerializeField] private float matConsumePerMin = 1f;
        [SerializeField] private float energyConsumePerMin = 3f;

        // —— 权威整数库存 ——
        private int _foodInt;
        private int _matInt;
        private int _energyInt;

        // —— 小数零头累加器（不对外暴露）——
        private float _foodFrac;
        private float _matFrac;
        private float _energyFrac;

        // —— 上次广播快照（避免频繁广播）——
        private int _lastFood = int.MinValue;
        private int _lastMat = int.MinValue;
        private int _lastEnergy = int.MinValue;

        #region ISimSystem
        public void Initialize()
        {
            _foodInt = Mathf.Clamp(foodCap / 2, 0, foodCap);
            _matInt = Mathf.Clamp(matCap / 2, 0, matCap);
            _energyInt = Mathf.Clamp(energyCap / 2, 0, energyCap);

            _foodFrac = _matFrac = _energyFrac = 0f;
            BroadcastIfChanged(force: true);
        }

        /// <param name="dm">Δ逻辑分钟，由时间系统传入</param>
        public void Tick(float dm)
        {
            // 1) 累加小数变化
            _foodFrac += (foodProducePerMin - foodConsumePerMin) * dm;
            _matFrac += (matProducePerMin - matConsumePerMin) * dm;
            _energyFrac += (energyProducePerMin - energyConsumePerMin) * dm;

            // 2) 将整份结转到整数库存，并钳制 0..cap
            AccumulateWhole(ref _foodFrac, ref _foodInt, foodCap);
            AccumulateWhole(ref _matFrac, ref _matInt, matCap);
            AccumulateWhole(ref _energyFrac, ref _energyInt, energyCap);

            // 3) 仅在整数库存变动时广播
            BroadcastIfChanged();
        }

        public void Shutdown() { }
        #endregion

        #region Event wiring
        private void OnEnable()
        {
            EventBus.Instance?.Subscribe<SpendResourcesRequest>(OnSpendResourcesRequest);

        }

        private void OnDisable()
        {
            if (EventBus.Instance == null) return;
            EventBus.Instance.Unsubscribe<SpendResourcesRequest>(OnSpendResourcesRequest);

        }
        #endregion

        #region Spend APIs
        /// <summary>
        /// 新版：按三资源一次性判定与扣费（只使用整数库存；小数零头不可用）
        /// </summary>
        private void OnSpendResourcesRequest(SpendResourcesRequest req)
        {
            int f = Mathf.Max(0, req.Food);
            int m = Mathf.Max(0, req.Mat);
            int e = Mathf.Max(0, req.Energy);

            bool affordable = _foodInt >= f && _matInt >= m && _energyInt >= e;

            if (affordable)
            {
                _foodInt -= f;
                _matInt -= m;
                _energyInt -= e;

                // 钳制非负
                _foodInt = Mathf.Max(0, _foodInt);
                _matInt = Mathf.Max(0, _matInt);
                _energyInt = Mathf.Max(0, _energyInt);

                BroadcastIfChanged(force: false);
            }

            EventBus.Instance?.Publish(new SpendResourcesResult
            {
                Ok = affordable,
                RemainFood = _foodInt,
                RemainMat = _matInt,
                RemainEnergy = _energyInt,
                TxId = req.TxId
            });
        }



        #region Helpers
        private static void AccumulateWhole(ref float fracAccu, ref int intStock, int cap)
        {
            // 使用 System.Math.Truncate 进行“向零取整”，与旧实现一致
            if (fracAccu >= 1f || fracAccu <= -1f)
            {
                int whole = (int)Math.Truncate(fracAccu); // 正负都支持
                fracAccu -= whole;
                intStock = Mathf.Clamp(intStock + whole, 0, cap);
            }
        }

        /// <summary>
        /// 只有当任意整数库存变化（或 force==true）才广播一次。
        /// </summary>
        private void BroadcastIfChanged(bool force = false)
        {
            if (force || _foodInt != _lastFood || _matInt != _lastMat || _energyInt != _lastEnergy)
            {
                _lastFood = _foodInt;
                _lastMat = _matInt;
                _lastEnergy = _energyInt;

                EventBus.Instance?.Publish(new ResourceChangedEvent
                {
                    Food = _foodInt,
                    Mat = _matInt,
                    Energy = _energyInt,
                    CapFood = foodCap,
                    CapMat = matCap,
                    CapEnergy = energyCap
                });
            }
        }
        #endregion

        #region (可选) 对外只读属性
        public int Food => _foodInt;
        public int Mat => _matInt;
        public int Energy => _energyInt;

        public int FoodCap => foodCap;
        public int MatCap => matCap;
        public int EnergyCap => energyCap;
        #endregion

        #region 兼容的内部状态类（如有 UI/存档引用可保留）
        [Serializable]
        public class Snapshot
        {
            public int food, mat, energy;
            public int capFood, capMat, capEnergy;
        }
        #endregion
    }

    // ====== 事件约定（若你已在 Core/Events.cs 定义，请删除这里或注释掉重复定义） ======
    // public struct SpendResourcesRequest { public int Food, Mat, Energy; public string TxId; }
    // public struct SpendResourcesResult { public bool Ok; public int RemainFood, RemainMat, RemainEnergy; public string TxId; }
    // public struct SpendEnergyRequest { public int Amount; public string TxId; }
    // public struct SpendEnergyResult { public bool Ok; public int Remaining; public string TxId; }
    // public struct ResourceChangedEvent { public int Food, Mat, Energy, CapFood, CapMat, CapEnergy; }
}
#endregion