// Assets/Scripts/Systems/ResourceSystem.cs
using ProjectSulamith.Core;
using System;
using System.Collections.Generic;
using UnityEngine;

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

        [SerializeField] private List<BuildingDef> buildingDefs; // 建筑配置列表（存放各建筑最大派遣人数）
        // prototypeId -> count
        private readonly System.Collections.Generic.Dictionary<string, int> _buildingCounts
            = new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.Ordinal);

        // 建筑ID -> 已派遣人数（核心派遣数据）
        private readonly Dictionary<string, int> _buildingTotalAssignPersons
            = new Dictionary<string, int>(StringComparer.Ordinal);

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

        private Dictionary<string, BuildingDef> _buildingDefMap;

        // 核心：每个建筑独立效率计算
        public float GetBuildingEfficiency(string buildingId)
        {
            if (!_buildingDefMap.TryGetValue(buildingId, out BuildingDef def))
                return 1f;

            int assigned = _buildingTotalAssignPersons.TryGetValue(buildingId, out int count) ? count : 0;

            // 效率 = 1 + 人数 * 每人效率（组长可在BuildingDef配置）
            float efficiency = 1f + assigned * def.efficiencyPerPerson;

            // 防止效率过低或过高（可根据需求调整）
            efficiency = Mathf.Max(efficiency, 0.1f);

            return efficiency;
        }
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
                    string buildingId = kv.Key;
                    int count = kv.Value;

                    if (count <= 0) continue;
                    if (!buildingYieldConfig.TryGet(buildingId, out var entry)) continue;

                    // 关键：每个建筑独立效率
                    float efficiency = GetBuildingEfficiency(buildingId);

                    food += entry.foodPerMin * count * efficiency;
                    mat += entry.matPerMin * count * efficiency;
                    energy += entry.energyPerMin * count * efficiency;
                }
            }
            // 扣除消耗
            food -= foodConsumePerMin;
            mat -= matConsumePerMin;
            energy -= energyConsumePerMin;

            // 确保产量不为负
            food = Mathf.Max(0f, food);
            mat = Mathf.Max(0f, mat);
            energy = Mathf.Max(0f, energy);

            return new Vector3(food, mat, energy);
        }

        #region ISimSystem
        public void Initialize()
        {
            _foodInt = Mathf.Clamp(foodCap / 2, 0, foodCap);
            _matInt = Mathf.Clamp(matCap / 2, 0, matCap);
            _energyInt = Mathf.Clamp(energyCap / 2, 0, energyCap);

            _foodFrac = _matFrac = _energyFrac = 0f;

            // 初始化BuildingDef字典
            InitBuildingDefMap();

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
            EventBus.Instance?.Subscribe<BuildingAssignPersonRequest>(OnBuildingAssignPersonRequest);
        }

        private void OnDisable()
        {
            if (EventBus.Instance == null) return;
            EventBus.Instance.Unsubscribe<SpendResourcesRequest>(OnSpendResourcesRequest);
            EventBus.Instance.Unsubscribe<BuildingPlacedEvent>(OnBuildingPlaced);
            EventBus.Instance.Unsubscribe<BuildingAssignPersonRequest>(OnBuildingAssignPersonRequest);
        }

        private void OnBuildingPlaced(BuildingPlacedEvent e)
        {
            if (string.IsNullOrEmpty(e.PrototypeId)) return;

            _buildingCounts.TryGetValue(e.PrototypeId, out int c);
            _buildingCounts[e.PrototypeId] = c + 1;

            // 可选：立即广播一次（让 UI 立刻看到“产出变化”——如果你 UI 有显示速率的话）
            // BroadcastIfChanged(force: true);
        }

        // 处理派遣人口请求
        private void OnBuildingAssignPersonRequest(BuildingAssignPersonRequest req)
        {
            if (req == null) return;

            // 1. 校验建筑是否存在
            if (!_buildingCounts.ContainsKey(req.PrototypeId) || _buildingCounts[req.PrototypeId] <= 0)
            {
                PublishAssignPersonResult(req, false, 0, 0);
                Debug.LogWarning($"建筑{req.PrototypeId}不存在，无法派遣人口");
                return;
            }

            // 2. 校验建筑最大派遣人数（解决CS0165报错：def未赋值）
            BuildingDef def = null;
            if (!_buildingDefMap.TryGetValue(req.PrototypeId, out def))
            {
                PublishAssignPersonResult(req, false, 0, 0);
                Debug.LogWarning($" 未找到建筑{req.PrototypeId}的配置，无法派遣人口");
                return;
            }

            if (req.TotalAssignPerson > def.maxAssignable)
            {
                PublishAssignPersonResult(req, false, 0, def.maxAssignable);
                Debug.LogWarning($"{req.PrototypeId}最大可派{def.maxAssignable}人，请求派遣{req.TotalAssignPerson}人");
                return;
            }

            // 3. 校验全局可派遣人口（健康人口）
            var populationSys = FindObjectOfType<PopulationSystem>(true);
            int maxAssignable = populationSys?.GetAssignablePopulation() ?? 0;
            if (req.TotalAssignPerson > maxAssignable)
            {
                PublishAssignPersonResult(req, false, 0, maxAssignable);
                Debug.LogWarning($" 全局可派遣人口不足！当前{maxAssignable}，请求{req.TotalAssignPerson}");
                return;
            }

            // 4. 更新派遣人数
            _buildingTotalAssignPersons[req.PrototypeId] = req.TotalAssignPerson;
            PublishAssignPersonResult(req, true, req.TotalAssignPerson, def.maxAssignable);

            // 5. 立即广播产量变化
            BroadcastIfChanged(force: true);
            Debug.Log($"{req.PrototypeId}派遣{req.TotalAssignPerson}人成功");
        }

        // 发布派遣结果
        private void PublishAssignPersonResult(BuildingAssignPersonRequest req, bool ok, int current, int max)
        {
            EventBus.Instance?.Publish(new BuildingAssignPersonResult
            {
                Ok = ok,
                PrototypeId = req.PrototypeId,
                CurrentTotalPerson = current,
                MaxTotalPerson = max,
                TxId = req.TxId
            });
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

                BroadcastIfChanged();
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
        // 初始化BuildingDef字典
        private void InitBuildingDefMap()
        {
            _buildingDefMap = new Dictionary<string, BuildingDef>(StringComparer.Ordinal);
            if (buildingDefs == null || buildingDefs.Count == 0)
            {
                Debug.LogWarning("未配置BuildingDef列表，请在Inspector中添加！");
                return;
            }

            foreach (var def in buildingDefs)
            {
                if (def == null || string.IsNullOrEmpty(def.id)) continue;
                _buildingDefMap[def.id] = def;
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

        // 对外暴露BuildingDef字典
        public Dictionary<string, BuildingDef> BuildingDefMap => _buildingDefMap;

        // 获取指定建筑的已派遣人数
        public int GetAssignedPersons(string prototypeId)
        {
            if (string.IsNullOrEmpty(prototypeId) || !_buildingTotalAssignPersons.ContainsKey(prototypeId))
                return 0;
            return _buildingTotalAssignPersons[prototypeId];
        }
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