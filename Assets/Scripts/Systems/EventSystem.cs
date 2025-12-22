// Assets/Scripts/Systems/EventsSystem.cs
using System;
using UnityEngine;

using UnityEngine.Tilemaps;
//所有事件放置的地方
namespace ProjectSulamith.Systems
{
    // ===== 资源变更广播：只广播整数库存，不暴露小数零头 =====
    public struct ResourceChangedEvent
    {
        public int Food, Mat, Energy, CapFood, CapMat, CapEnergy;
    }

    // ===== 统一的“三资源扣费”请求/结果 =====
    public struct SpendResourcesRequest
    {
        // 申请扣费（均为整数；小于0按0处理由 ResourceSystem 决定）
        public int Food;
        public int Mat;
        public int Energy;

        // 事务ID，用于一一对应请求与结果
        public Guid TxId;
    }

    public struct SpendResourcesResult
    {
        // 是否成功扣费（全部资源一次性判定与扣减）
        public bool Ok;

        // 扣减后的剩余（或失败时的当前值）
        public int RemainFood;
        public int RemainMat;
        public int RemainEnergy;

        // 回传事务ID
        public Guid TxId;
    }

    

    // ===== 示例：建造请求/结果（上层系统可以直接改为三资源成本） =====
    public struct BuildRequest
    {
        public string PrototypeId;//名称
        public Vector3Int CellPosition;//位置

        
        public int FoodCost;
        public int MatCost;
        public int EnergyCost;
        //三种资源成本
        public Guid TxId;
    }

    public struct BuildAccepted
    {
        public string PrototypeId;
        public Vector3Int CellPosition;
        public Guid TxId;
    }

    public struct BuildRejected
    {
        public string PrototypeId;
        public Vector3Int CellPosition;
        public string Reason;
        public Guid TxId;
    }

    public struct BuildingPlacedEvent
    {
        public Vector3Int CellPosition;
        public string PrototypeId;
    }

    // ===== 建造流程中用到的事件（保持不变，可继续使用） =====
    public struct BuildRequestedEvent { public BuildingDef def; }
    public struct BuildQueuedEvent { public BuildingDef def; public int queueLength; }
    public struct BuildStartedEvent { public BuildingDef def; }
    public struct BuildProgressEvent { public BuildingDef def; public float tNorm; } // 0-1
    public struct BuildCompletedEvent { public BuildingDef def; }
    public struct BuildFailedEvent { public BuildingDef def; public string reason; }
}
