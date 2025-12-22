using System.Collections.Generic;
using UnityEngine;

namespace ProjectSulamith.TechTree
{
    // 单个科技节点的数据
    [CreateAssetMenu(
        fileName = "TechNode_新科技",
        menuName = "ProjectSulamith/TechTree/Tech Node")]
    public class TechNodeData : ScriptableObject
    {
        [Header("基础信息")]
        public string id;                // 唯一 ID，用来存档、解锁判断
        public string displayName;       // 展示名
        [TextArea(2, 5)]
        public string description;       // 说明文案

        public TechCategory category;    // 分类：生存/工程/医疗等
        public int cost;                 // 消耗点数（之后可以和资源系统接）

        [Header("布局（UI 坐标，单位：像素）")]
        public Vector2 uiPosition;       // 在星图中的位置

        [Header("前置科技")]
        public List<TechNodeData> prerequisites = new List<TechNodeData>();
    }

    public enum TechCategory
    {
        Survival,
        Engineering,
        Medical,
        Communication,
        Drainage,
        Weather,
        Hull,
        StormPrep
    }
}
