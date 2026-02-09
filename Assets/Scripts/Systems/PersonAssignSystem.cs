using UnityEngine;
using ProjectSulamith.Core;
using System;

namespace ProjectSulamith.Systems
{
    /// <summary>
    /// 人口派遣系统：管理玩家派遣人口到建筑的逻辑（校验+转发请求）
    /// </summary>
    public class PersonAssignSystem : MonoBehaviour
    {
        // 缓存其他核心系统引用（避免频繁Find）
        private ResourceSystem _resourceSystem;
        private PopulationSystem _populationSystem;

        private void Awake()
        {
            // 初始化系统引用（场景中需有这两个系统的实例）
            _resourceSystem = FindObjectOfType<ResourceSystem>(true);
            _populationSystem = FindObjectOfType<PopulationSystem>(true);

            // 校验系统是否存在，避免空指针
            if (_resourceSystem == null)
                Debug.LogError("场景中未找到ResourceSystem，请先添加！");
            if (_populationSystem == null)
                Debug.LogError("场景中未找到PopulationSystem，请先添加！");
        }

        #region 对外API：派遣人口到指定建筑（供UI/测试脚本调用）
        /// <summary>
        /// 派遣人口到指定建筑
        /// </summary>
        /// <param name="prototypeId">建筑原型ID（如Warehouse）</param>
        /// <param name="assignCount">要派遣的人数</param>
        /// <returns>派遣结果（是否成功）</returns>
        public BuildingAssignPersonResult AssignPersonToBuilding(string prototypeId, int assignCount)
        {
            // 1. 基础校验：参数/系统是否合法
            if (string.IsNullOrEmpty(prototypeId) || assignCount <= 0)
            {
                return new BuildingAssignPersonResult
                {
                    Ok = false,
                    PrototypeId = prototypeId,
                    CurrentTotalPerson = 0,
                    MaxTotalPerson = 0,
                    TxId = Guid.NewGuid().ToString()
                };
            }

            if (_resourceSystem == null || _populationSystem == null)
            {
                Debug.LogError("核心系统未初始化，无法派遣人口！");
                return new BuildingAssignPersonResult { Ok = false, PrototypeId = prototypeId };
            }

            // 2. 校验：可派遣人口是否足够
            int assignablePopulation = _populationSystem.GetAssignablePopulation();
            if (assignCount > assignablePopulation)
            {
                Debug.LogWarning($"可派遣人口不足！当前可派遣：{assignablePopulation}，请求派遣：{assignCount}");
                return new BuildingAssignPersonResult
                {
                    Ok = false,
                    PrototypeId = prototypeId,
                    CurrentTotalPerson = 0,
                    MaxTotalPerson = assignablePopulation,
                    TxId = Guid.NewGuid().ToString()
                };
            }

            // 3. 发布派遣请求，让ResourceSystem处理（复用之前的事件逻辑）
            var request = new BuildingAssignPersonRequest
            {
                PrototypeId = prototypeId,
                TotalAssignPerson = assignCount,
                TxId = Guid.NewGuid().ToString()
            };

            EventBus.Instance?.Publish(request);
            _populationSystem.AssignPopulation(assignCount);
            // 4. 模拟返回结果（也可订阅ResourceSystem的返回事件，这里简化）
            // 实际项目中可通过EventBus订阅BuildingAssignPersonResult获取最终结果
            return new BuildingAssignPersonResult
            {
                Ok = true,
                PrototypeId = prototypeId,
                CurrentTotalPerson = assignCount,
                MaxTotalPerson = assignablePopulation,
                TxId = request.TxId
            };
        }
        /// <summary>
        /// 撤回指定数量的人口（新增重载，支持两个参数）
        /// </summary>
        public bool WithdrawPersonFromBuilding(string prototypeId, int withdrawCount)
        {
            if (string.IsNullOrEmpty(prototypeId) || _populationSystem == null || withdrawCount <= 0)
                return false;

            // 调用人口系统回收
            bool success = _populationSystem.TakeBackPopulation(withdrawCount);

            if (success)
            {
                // 发布事件通知其他系统
                EventBus.Instance?.Publish(new BuildingAssignPersonRequest
                {
                    PrototypeId = prototypeId,
                    TotalAssignPerson = -withdrawCount, // 负数表示撤回
                    TxId = Guid.NewGuid().ToString()
                });
            }

            return success;
        }
        #endregion
    }

}