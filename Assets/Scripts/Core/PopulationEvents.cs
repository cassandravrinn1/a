using System;

namespace ProjectSulamith.Core
{
    /// <summary>
    /// 人口状态变化事件：全局广播人口总数/各状态人数变化
    /// </summary>
    [Serializable]
    public class PopulationChangedEvent
    {
        public int TotalPopulation; // 营地总人数
        public int Healthy; // 健康人数（可派遣）
        public int Sick;    // 生病人数（不可派遣）
        public int Hungry;  // 饥饿人数（派遣后效率减半）
        public int Assignable; // 可派遣人口数（健康人数）
    }

    /// <summary>
    /// 人口增减请求事件：供其他系统（如民居建筑）调用
    /// </summary>
    public class PopulationAddRequest
    {
        public int Count; // 新增人数
        public string TxId = Guid.NewGuid().ToString();
    }
}