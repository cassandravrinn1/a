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

        [Header("Base per minute (optional)")]
        [SerializeField] private float baseFoodPerMin = 0f;
        [SerializeField] private float baseMatPerMin = 0f;
        [SerializeField] private float baseEnergyPerMin = 0f;

        [Header("Building yields")]
        [SerializeField] private BuildingYieldConfig buildingYieldConfig;

        // prototypeId -> count
        private readonly System.Collections.Generic.Dictionary<string, int> _buildingCounts
            = new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.Ordinal);


        [Header("Consume per minute（可小数）")]
        [SerializeField] private float foodConsumePerMin = 2f;
        [SerializeField] private float matConsumePerMin = 1f;
        [SerializeField] private float energyConsumePerMin = 3f;

        // —— 整数库存 ——
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

        public void Tick(float dm)
        {
            var rate = ComputeNetRatePerMin(); // (food, mat, energy) 每分钟净变化

            _foodFrac += rate.x * dm;
            _matFrac += rate.y * dm;
            _energyFrac += rate.z * dm;

            AccumulateWhole(ref _foodFrac, ref _foodInt, foodCap);
            AccumulateWhole(ref _matFrac, ref _matInt, matCap);
            AccumulateWhole(ref _energyFrac, ref _energyInt, energyCap);

            BroadcastIfChanged();
        }

        private Vector3 ComputeNetRatePerMin()
        {
            float food = baseFoodPerMin;
            float mat = baseMatPerMin;
            float energy = baseEnergyPerMin;

            if (buildingYieldConfig != null)
            {
                foreach (var kv in _buildingCounts)
                {
                    if (kv.Value <= 0) continue;
                    if (!buildingYieldConfig.TryGet(kv.Key, out var entry)) continue;

                    food += entry.foodPerMin * kv.Value;
                    mat += entry.matPerMin * kv.Value;
                    energy += entry.energyPerMin * kv.Value;
                }
            }

            return new Vector3(food, mat, energy);
        }

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
        

        public void Shutdown() { }
        #endregion

        #region Event 
        private void OnEnable()
        {
            EventBus.Instance?.Subscribe<SpendResourcesRequest>(OnSpendResourcesRequest);
            EventBus.Instance?.Subscribe<BuildingPlacedEvent>(OnBuildingPlaced);
        }

        private void OnDisable()
        {
            if (EventBus.Instance == null) return;
            EventBus.Instance.Unsubscribe<SpendResourcesRequest>(OnSpendResourcesRequest);
            EventBus.Instance.Unsubscribe<BuildingPlacedEvent>(OnBuildingPlaced);
        }

        private void OnBuildingPlaced(BuildingPlacedEvent e)
        {
            if (string.IsNullOrEmpty(e.PrototypeId)) return;

            _buildingCounts.TryGetValue(e.PrototypeId, out int c);
            _buildingCounts[e.PrototypeId] = c + 1;

            // 可选：立即广播一次（让 UI 立刻看到“产出变化”——如果你 UI 有显示速率的话）
            // BroadcastIfChanged(force: true);
        }

        #endregion

        #region Spend APIs
        /// <summary>
        /// 按三资源一次性判定与扣费（只使用整数库存；小数零头不可用）
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

    
}
#endregion