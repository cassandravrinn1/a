using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ProjectSulamith.Core;
using ProjectSulamith.Systems;

public class TestBuildPanel : MonoBehaviour
{
    // 建筑预制体引用
    [Header("Building Prefabs")]
    public GameObject warehousePrefab;
    public GameObject batteryPrefab;
    public GameObject canteenPrefab;

    [Header("Refs")]
    public TMP_Text resText;       // 显示三资源与上限
    public TMP_Text logText;       // 事件日志（可选）
    public Button btnWarehouse;
    public Button btnBattery;
    public Button btnCanteen;

    //public Button btnOpenAssignPanel; // 打开派遣面板的按钮(是否需要单独派遣面板待定)
    public BuildingAssignUI assignUI; // 派遣UI引用

    [Header("Costs (整数)")]
    public int warehouseFood = 0;
    public int warehouseMat = 30;
    public int warehouseEnergy = 10;

    public int batteryFood = 0;
    public int batteryMat = 10;
    public int batteryEnergy = 40;

    public int canteenFood = 20;
    public int canteenMat = 10;
    public int canteenEnergy = 5;

    public HexGridData hexGrid;

    private EventBus _bus;

    void OnEnable()
    {
        _bus = EventBus.Instance;
        _bus?.Subscribe<ResourceChangedEvent>(OnResChanged);
        _bus?.Subscribe<BuildAccepted>(OnBuildAccepted);
        _bus?.Subscribe<BuildRejected>(OnBuildRejected);

        if (btnWarehouse) btnWarehouse.onClick.AddListener(() => RequestBuild("Warehouse", warehouseFood, warehouseMat, warehouseEnergy));
        if (btnBattery) btnBattery.onClick.AddListener(() => RequestBuild("Battery", batteryFood, batteryMat, batteryEnergy));
        if (btnCanteen) btnCanteen.onClick.AddListener(() => RequestBuild("Canteen", canteenFood, canteenMat, canteenEnergy));
        /*
        // 绑定打开派遣面板按钮
        if (btnOpenAssignPanel)
        {
            btnOpenAssignPanel.onClick.AddListener(OnOpenAssignPanel);
            btnOpenAssignPanel.interactable = false; // 初始禁用（未选中有建筑的格子）
        }*/
    }

    void OnDisable()
    {
        _bus?.Unsubscribe<ResourceChangedEvent>(OnResChanged);
        _bus?.Unsubscribe<BuildAccepted>(OnBuildAccepted);
        _bus?.Unsubscribe<BuildRejected>(OnBuildRejected);
        _bus = null;

        if (btnWarehouse) btnWarehouse.onClick.RemoveAllListeners();
        if (btnBattery) btnBattery.onClick.RemoveAllListeners();
        if (btnCanteen) btnCanteen.onClick.RemoveAllListeners();

        //if (btnOpenAssignPanel) btnOpenAssignPanel.onClick.RemoveAllListeners();
    }
    /*
    // 每帧检测选中格子是否有建筑，更新派遣按钮状态
    void Update()
    {
        UpdateAssignButtonState();
    }

    // 更新派遣按钮是否可用
    private void UpdateAssignButtonState()
    {
        if (btnOpenAssignPanel == null || hexGrid == null) return;

        HexTileData selectedTile = hexGrid.GetSelectedTile();
        // 只有选中有建筑的格子，按钮才可用
        btnOpenAssignPanel.interactable = (selectedTile != null && selectedTile.hasBuilding && !string.IsNullOrEmpty(selectedTile.buildingPrototypeId));
    }
    // 打开派遣面板
    private void OnOpenAssignPanel()
    {
        HexTileData selectedTile = hexGrid.GetSelectedTile();
        if (selectedTile == null || !selectedTile.hasBuilding)
        {
            Log(" 未选中有建筑的格子！");
            return;
        }

        if (assignUI == null)
        {
            Log(" 未绑定派遣UI！");
            return;
        }

        // 弹出派遣面板，传入选中建筑的ID
        assignUI.ShowPanel(selectedTile.buildingPrototypeId);
        Log($" 打开{selectedTile.buildingPrototypeId}的派遣面板");
    }
     */
    private void OnResChanged(ResourceChangedEvent e)
    {
        if (resText)
            resText.text = $"Food {e.Food}/{e.CapFood} | Mat {e.Mat}/{e.CapMat} | Energy {e.Energy}/{e.CapEnergy}";
    }
    //资源显示

    private void RequestBuild(string proto, int f, int m, int en)
    {
        var tile = hexGrid.GetSelectedTile();
        if (tile == null || tile.hasBuilding)
        {
            Log("No valid tile selected");
            return;
        }

        var tx = Guid.NewGuid();

        EventBus.Instance?.Publish(new BuildRequest
        {
            PrototypeId = proto,
            CellPosition = tile.cellPosition,
            FoodCost = f,
            MatCost = m,
            EnergyCost = en,
            TxId = tx
        });

        // 建造成功后，记录格子的建筑ID（派遣可用）
        tile.hasBuilding = true;
        tile.buildingPrototypeId = proto;

        // 建造建筑实例（关键：给建筑挂载点击脚本并赋值）
        SpawnBuildingInstance(proto, tile);
    }

    // 生成建筑实例并配置点击脚本
    private void SpawnBuildingInstance(string proto, HexTileData tile)
    {
        GameObject prefab = null;
        switch (proto)
        {
            case "Warehouse": prefab = warehousePrefab; break;
            case "Battery": prefab = batteryPrefab; break;
            case "Canteen": prefab = canteenPrefab; break;
            default: Debug.LogWarning($"未知建筑原型：{proto}"); return;
        }

        if (prefab == null)
        {
            Debug.LogWarning($"建筑预制体未绑定：{proto}");
            return;
        }

        // 生成建筑实例（放在格子中心位置）
        Vector3 worldPos = hexGrid.GetCellCenterWorld(tile.cellPosition);
        GameObject buildingObj = Instantiate(prefab, worldPos, Quaternion.identity);
        buildingObj.name = $"{proto}_{tile.cellPosition}";

        // 给建筑添加点击脚本并赋值关键参数
        BuildingClickTrigger clickTrigger = buildingObj.GetComponent<BuildingClickTrigger>();
        if (clickTrigger == null)
        {
            clickTrigger = buildingObj.AddComponent<BuildingClickTrigger>();
        }
        clickTrigger.cellPosition = tile.cellPosition;
        clickTrigger.buildingPrototypeId = proto;

        // 记录建筑实例到格子数据中
        tile.buildingInstance = buildingObj;
    }
    private void OnBuildAccepted(BuildAccepted e)
    {
        Log($"Accepted {e.PrototypeId}  ");
        // 如需队列/计时，这里也可触发后续 UI
    }

    private void OnBuildRejected(BuildRejected e)
    {
        Log($"Rejected {e.PrototypeId}  reason={e.Reason}  ");
    }

    private void Log(string line)
    {
        if (!logText) return;
        logText.text = (line + "\n" + logText.text);
    }
}
