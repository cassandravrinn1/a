using UnityEngine;
using ProjectSulamith.Systems;

// 挂载到所有建筑预制体上（Warehouse/Battery/Canteen）
public class BuildingClickTrigger : MonoBehaviour
{
    [Tooltip("建筑对应的格子坐标（由建造系统赋值）")]
    public Vector3Int cellPosition;

    [Tooltip("建筑原型ID（Warehouse/Battery/Canteen）")]
    public string buildingPrototypeId;

    private HexGridData _hexGridData;
    private TestBuildPanel _buildPanel;

    void Awake()
    {
        // 自动查找全局的HexGridData和TestBuildPanel
        _hexGridData = FindObjectOfType<HexGridData>(true);
        _buildPanel = FindObjectOfType<TestBuildPanel>(true);

        // 给建筑加碰撞体（确保能检测点击）
        if (GetComponent<Collider>() == null)
        {
            Collider collider = gameObject.AddComponent<BoxCollider>();
            (collider as BoxCollider).isTrigger = false; // 非触发器，用于射线检测
        }
    }

    // 检测鼠标点击
    void OnMouseDown()
    {
        OnBuildingClicked();
    }

    // 建筑被点击后的核心逻辑
    public void OnBuildingClicked()
    {
        if (_hexGridData == null || _buildPanel == null)
        {
            Debug.LogWarning($"[{buildingPrototypeId}] 找不到HexGridData或TestBuildPanel！");
            return;
        }
        Debug.Log($"尝试打开派遣面板：{buildingPrototypeId}");
        // 1. 选中该建筑所在的格子（联动HexGridData）
        _hexGridData.SelectCell(cellPosition);

        // 2. 直接打开派遣面板（无需点击按钮）
        var selectedTile = _hexGridData.GetSelectedTile();
        if (selectedTile != null && selectedTile.hasBuilding && _buildPanel.assignUI != null)
        {
            _buildPanel.assignUI.ShowPanel(buildingPrototypeId);
            Debug.Log($"点击建筑{buildingPrototypeId}，直接打开派遣面板");
        }
        else
        {
            Debug.LogWarning($"[{buildingPrototypeId}] 建筑格子数据异常，无法打开派遣面板");
        }
    }
}