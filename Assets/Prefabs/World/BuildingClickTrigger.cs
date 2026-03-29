using UnityEngine;
using ProjectSulamith.Systems;

public class BuildingClickTrigger : MonoBehaviour
{
    public Vector3Int cellPosition;
    public string buildingPrototypeId;
    public string buildingInstanceId;


    void Awake()
    {
        if (GetComponent<Collider2D>() == null)
        {
            BoxCollider2D collider2D = gameObject.AddComponent<BoxCollider2D>();
            collider2D.isTrigger = false;
            collider2D.size = new Vector2(1, 1);
        }
        // 关键：确保物体能接收鼠标事件
        gameObject.GetComponent<Collider2D>().enabled = true;
        gameObject.layer = LayerMask.NameToLayer("Default");
    }


    // 兜底保留OnMouseDown
    void OnMouseDown()
    {
        if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            return;

        if (IsBuildingInProgress())
        {
            Debug.Log($"【ClickTrigger】建筑 {buildingInstanceId} 正在建造中，禁止点击");
            return; // 直接返回，不执行后续逻辑
        }
        // 日志验证ID
        Debug.Log($"【ClickTrigger】点击建筑，PrototypeId: {buildingPrototypeId} | InstanceId: {buildingInstanceId}");

        // 调用管理器打开派遣面板
        if (PopupRootManager.Instance != null)
        {
            PopupRootManager.Instance.ShowBuildingAssignUI(buildingInstanceId);
        }
    }
    /// <summary>
    /// 判断当前建筑是否处于建造中状态
    /// </summary>
    private bool IsBuildingInProgress()
    {
        // 步骤1：找到全局的HexGridData（存储所有地块/建筑状态）
        HexGridData hexGrid = FindObjectOfType<HexGridData>(true);
        if (hexGrid == null)
        {
            Debug.LogWarning("[ClickTrigger] 找不到HexGridData实例，默认允许点击");
            return false;
        }

        // 步骤2：根据cellPosition获取地块数据
        HexTileData tileData = hexGrid.GetTileData(cellPosition);
        if (tileData == null)
        {
            Debug.LogWarning($"[ClickTrigger] 地块 {cellPosition} 数据为空，默认允许点击");
            return false;
        }

        // 步骤3：判断该地块的建筑是否在建造中
        // 条件：isBuilding为true → 建造中；false → 建造完成
        return tileData.isBuilding;
    }
}