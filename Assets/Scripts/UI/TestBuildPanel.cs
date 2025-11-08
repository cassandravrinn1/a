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

    void OnEnable()
    {
        EventBus.Instance?.Subscribe<ResourceChangedEvent>(OnResChanged);
        EventBus.Instance?.Subscribe<BuildAccepted>(OnBuildAccepted);
        EventBus.Instance?.Subscribe<BuildRejected>(OnBuildRejected);

        if (btnWarehouse) btnWarehouse.onClick.AddListener(() => RequestBuild("Warehouse", warehouseFood, warehouseMat, warehouseEnergy));
        if (btnBattery) btnBattery.onClick.AddListener(() => RequestBuild("Battery", batteryFood, batteryMat, batteryEnergy));
        if (btnCanteen) btnCanteen.onClick.AddListener(() => RequestBuild("Canteen", canteenFood, canteenMat, canteenEnergy));
    }

    void OnDisable()
    {
        if (EventBus.Instance == null) return;
        EventBus.Instance.Unsubscribe<ResourceChangedEvent>(OnResChanged);
        EventBus.Instance.Unsubscribe<BuildAccepted>(OnBuildAccepted);
        EventBus.Instance.Unsubscribe<BuildRejected>(OnBuildRejected);

        if (btnWarehouse) btnWarehouse.onClick.RemoveAllListeners();
        if (btnBattery) btnBattery.onClick.RemoveAllListeners();
        if (btnCanteen) btnCanteen.onClick.RemoveAllListeners();
    }

    private void OnResChanged(ResourceChangedEvent e)
    {
        if (resText)
            resText.text = $"Food {e.Food}/{e.CapFood} | Mat {e.Mat}/{e.CapMat} | Energy {e.Energy}/{e.CapEnergy}";
    }

    private void RequestBuild(string proto, int f, int m, int en)
    {
        var tx = Guid.NewGuid();
        Log($"> BuildRequest {proto}  Cost Food{f}/Material{m}/Energy{en}  ");
        //tx ={ tx}
        EventBus.Instance?.Publish(new BuildRequest
        {
            PrototypeId = proto,
            FoodCost = Mathf.Max(0, f),
            MatCost = Mathf.Max(0, m),
            EnergyCost = Mathf.Max(0, en),
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
