using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ProjectSulamith.Core;
using ProjectSulamith.Systems;

public class TestBuildPanel : MonoBehaviour
{
    [Header("Refs")]
    public TMP_Text resText;       // 显示三资源与上限
    public TMP_Text logText;       // 事件日志（可选）
    public Button btnWarehouse;
    public Button btnBattery;
    public Button btnCanteen;

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
    }


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
