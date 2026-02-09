// ProjectSulamith/Core/BuildingAssignEvents.cs
using System;

namespace ProjectSulamith.Core
{
    // 建筑派遣人口请求
    public class BuildingAssignPersonRequest
    {
        public string PrototypeId;
        public int TotalAssignPerson;
        public string TxId = Guid.NewGuid().ToString();
    }

    // 建筑派遣人口结果
    public class BuildingAssignPersonResult
    {
        public bool Ok;
        public string PrototypeId;
        public int CurrentTotalPerson;
        public int MaxTotalPerson;
        public string TxId;
    }
}