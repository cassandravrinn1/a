using ProjectSulamith.Core;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectSulamith.Systems
{
    /// <summary>
    /// 人口系统（权威）：管理全局人口总数、状态（健康/疾病/饥饿）、人口增减
    /// </summary>
    public class PopulationSystem : MonoBehaviour, ISimSystem
    {
        [Header("初始人口配置")]
        [SerializeField] private int initialTotalPopulation = 20; // 营地初始总人数
        [SerializeField] private int initialHealthy = 20; // 初始健康人数
        [SerializeField] private int initialSick = 0; // 初始生病人数
        [SerializeField] private int initialHungry = 0; // 初始饥饿人数

        [Header("人口状态变化速率（每分钟）")]
        [Tooltip("饥饿人数增长速率（和资源系统的食物库存挂钩）")]
        [SerializeField] private float hungryRatePerMin = 0.5f;
        [Tooltip("生病人数增长速率（和资源系统的医疗资源/卫生挂钩，先占位）")]
        [SerializeField] private float sickRatePerMin = 0.1f;
        [Tooltip("康复速率（健康人数增加，生病人数减少）")]
        [SerializeField] private float healRatePerMin = 0.2f;

        // 核心状态：人口总数（健康+生病+饥饿，无重叠）
        private int _totalPopulation;
        private int _healthy; // 健康（可派遣）
        private int _sick;    // 生病（不可派遣）
        private int _hungry;  // 饥饿（效率减半）
        private int _assignedPopulation;

        // 缓存上次状态，避免频繁广播
        private (int total, int healthy, int sick, int hungry) _lastState;

        // 事件订阅：监听资源变化（食物不足会导致饥饿）
        private void OnEnable()
        {
            EventBus.Instance?.Subscribe<ResourceChangedEvent>(OnResourceChanged);
        }

        private void OnDisable()
        {
            if (EventBus.Instance == null) return;
            EventBus.Instance.Unsubscribe<ResourceChangedEvent>(OnResourceChanged);
        }

        #region ISimSystem 接口（和ResourceSystem一致，由时间系统驱动）
        public void Initialize()
        {
            // 初始化人口状态，保证数值合法（非负、总数匹配）
            _totalPopulation = Mathf.Max(initialTotalPopulation, 0);
            _healthy = Mathf.Clamp(initialHealthy, 0, _totalPopulation);
            _sick = Mathf.Clamp(initialSick, 0, _totalPopulation - _healthy);
            _hungry = Mathf.Clamp(initialHungry, 0, _totalPopulation - _healthy - _sick);

            // 修正总数（避免配置错误导致总数不一致）
            _totalPopulation = _healthy + _sick + _hungry;
            _lastState = (_totalPopulation, _healthy, _sick, _hungry);

            // 广播初始状态
            BroadcastPopulationChanged();
        }

        /// <summary>
        /// 时间心跳：更新人口状态（每分钟变化）
        /// </summary>
        /// <param name="dm">逻辑分钟增量（和ResourceSystem的dm一致）</param>
        public void Tick(float dm)
        {
            // 1. 更新饥饿状态（食物不足时加速增长）
            UpdateHungryState(dm);
            // 2. 更新生病/康复状态
            UpdateSickState(dm);
            // 3. 保证数值合法（非负、不超过总数）
            ValidatePopulationState();
            // 4. 状态变化则广播
            BroadcastIfChanged();
        }

        public void Shutdown() { }
        #endregion

        #region 核心逻辑：人口状态更新
        /// <summary>
        /// 更新饥饿状态（核心关联ResourceSystem的食物库存）
        /// </summary>
        private void UpdateHungryState(float dm)
        {
            // 获取资源系统的食物库存（通过全局查找，或注入引用，这里用查找更快捷）
            if (!FindObjectOfType<ResourceSystem>(true).TryGetComponent(out ResourceSystem resourceSys))
                return;

            float hungryDelta = hungryRatePerMin * dm;
            // 食物库存为0时，饥饿速率翻倍
            if (resourceSys.Food <= 0)
                hungryDelta *= 2f;

            // 饥饿人数变化（取整，避免小数）
            int hungryChange = Mathf.RoundToInt(hungryDelta);
            if (hungryChange <= 0) return;

            // 从健康人口中转化为饥饿（优先健康人口）
            int availableHealthy = Mathf.Max(_healthy - hungryChange, 0);
            _hungry += hungryChange - (_healthy - availableHealthy);
            _healthy = availableHealthy;
        }

        /// <summary>
        /// 更新生病/康复状态
        /// </summary>
        private void UpdateSickState(float dm)
        {
            // 生病人数增长
            int sickDelta = Mathf.RoundToInt(sickRatePerMin * dm);
            if (sickDelta > 0)
            {
                int availableHealthy = Mathf.Max(_healthy - sickDelta, 0);
                _sick += sickDelta - (_healthy - availableHealthy);
                _healthy = availableHealthy;
            }

            // 康复人数增长（从生病人口中恢复）
            int healDelta = Mathf.RoundToInt(healRatePerMin * dm);
            if (healDelta > 0)
            {
                int availableSick = Mathf.Max(_sick - healDelta, 0);
                _healthy += healDelta - (_sick - availableSick);
                _sick = availableSick;
            }
        }

        /// <summary>
        /// 校验人口状态：保证所有数值非负、总数一致
        /// </summary>
        private void ValidatePopulationState()
        {
            _healthy = Mathf.Max(_healthy, 0);
            _sick = Mathf.Max(_sick, 0);
            _hungry = Mathf.Max(_hungry, 0);

            // 重新计算总数，避免配置/逻辑错误导致总数不一致
            _totalPopulation = _healthy + _sick + _hungry;
        }
        #endregion

        #region 对外API：人口增减/状态修改（供其他系统调用）
        /// <summary>
        /// 新增人口（比如建造民居、完成任务）
        /// </summary>
        /// <param name="count">新增人数（默认新增健康人口）</param>
        public void AddPopulation(int count)
        {
            if (count <= 0) return;
            _healthy += count;
            _totalPopulation += count;
            ValidatePopulationState();
            BroadcastIfChanged();
        }

        /// <summary>
        /// 减少人口（比如死亡、迁移）
        /// </summary>
        /// <param name="count">减少人数（优先减少生病/饥饿人口）</param>
        public void RemovePopulation(int count)
        {
            if (count <= 0 || _totalPopulation <= 0) return;

            // 优先减少生病人口
            int removeFromSick = Mathf.Min(count, _sick);
            _sick -= removeFromSick;
            count -= removeFromSick;

            // 再减少饥饿人口
            if (count > 0)
            {
                int removeFromHungry = Mathf.Min(count, _hungry);
                _hungry -= removeFromHungry;
                count -= removeFromHungry;
            }

            // 最后减少健康人口
            if (count > 0)
            {
                _healthy = Mathf.Max(_healthy - count, 0);
            }

            _totalPopulation = _healthy + _sick + _hungry;
            ValidatePopulationState();
            BroadcastIfChanged();
        }

        /// <summary>
        /// 获取可派遣人口（健康人口，饥饿/生病不可派遣）
        /// </summary>
        public int GetAssignablePopulation()
        {
            return Mathf.Max(_healthy - _assignedPopulation, 0);
        }
        #endregion

        #region 事件广播：状态变化通知UI/其他系统
        private void BroadcastIfChanged()
        {
            var currentState = (_totalPopulation, _healthy, _sick, _hungry);
            if (currentState != _lastState)
            {
                _lastState = currentState;
                BroadcastPopulationChanged();
            }
        }

        private void BroadcastPopulationChanged()
        {
            EventBus.Instance?.Publish(new PopulationChangedEvent
            {
                TotalPopulation = _totalPopulation,
                Healthy = _healthy,
                Sick = _sick,
                Hungry = _hungry,
                Assignable = GetAssignablePopulation()
            });
        }
        #endregion

        #region 事件处理：监听资源变化（比如食物不足）
        private void OnResourceChanged(ResourceChangedEvent e)
        {
            // 食物库存变化时，触发一次状态检查（可选）
            ValidatePopulationState();
            BroadcastIfChanged();
        }
        #endregion

        #region 对外只读属性（供UI/其他系统读取）
        public int TotalPopulation => _totalPopulation;
        public int Healthy => _healthy;
        public int Sick => _sick;
        public int Hungry => _hungry;
        #endregion
        /// <summary>
        /// 分配人口（派遣系统调用）
        /// </summary>
        public bool AssignPopulation(int count)
        {
            if (count <= 0) return false;
            if (_assignedPopulation + count > _healthy) return false;

            _assignedPopulation += count;
            BroadcastIfChanged();
            return true;
        }

        /// <summary>
        /// 回收人口（撤回时调用）
        /// </summary>
        public bool TakeBackPopulation(int count)
        {
            if (count <= 0) return false;
            if (_assignedPopulation < count) return false;

            _assignedPopulation -= count;
            BroadcastIfChanged();
            return true;
        }
    }


}