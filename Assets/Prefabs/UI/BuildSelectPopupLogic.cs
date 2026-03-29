using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ProjectSulamith.Core;
using System.Collections;
using ProjectSulamith.Systems;

/// <summary>
/// 建造选择弹窗逻辑：点击地块后弹出，选择建造的建筑类型
/// 挂载在 BuildSelectPopup 预制体上
/// </summary>
public class BuildSelectPopupLogic : MonoBehaviour
{
    [Header("Building Configs (关联SO配置)")]
    public BuildingDef warehouseDef; // 仓库配置
    public BuildingDef batteryDef;   // 电池配置
    public BuildingDef canteenDef;   // 食堂配置
    // 【和原有 TestBuildPanel 一致的配置】
    [Header("Building Prefabs")]
    public GameObject warehousePrefab;
    public GameObject batteryPrefab;
    public GameObject canteenPrefab;

    [Header("Build Costs (整数)")]
    public int warehouseFood = 0;
    public int warehouseMat = 30;
    public int warehouseEnergy = 10;

    public int batteryFood = 0;
    public int batteryMat = 10;
    public int batteryEnergy = 40;

    public int canteenFood = 20;
    public int canteenMat = 10;
    public int canteenEnergy = 5;

    [Header("Build Duration (秒)")]
    public float warehouseDuration; 
    public float batteryDuration; 
    public float canteenDuration ;

    [Header("UI组件")]
    public Button btnWarehouse;
    public Button btnBattery;
    public Button btnCanteen;
    public Button btnCancel;
    public TMP_Text logText; // 可选：弹窗内显示建造日志

    // 临时存储选中的地块
    private HexTileData _selectedTile;
    private HexGridData _hexGrid;
    private EventBus _eventBus;

    private void Awake()
    {
        // 初始隐藏弹窗
        HidePanel();
        // 获取全局引用
        _hexGrid = FindObjectOfType<HexGridData>(true);
        _eventBus = EventBus.Instance;
    }

    private void Start()
    {
        // 绑定按钮事件
        BindButtonEvents();
    }

    /// <summary>
    /// 外部调用：打开建造弹窗（传入选中的地块）
    /// </summary>
    public void ShowPanel(HexTileData selectedTile)
    {
        // 校验地块是否可建造
        if (selectedTile == null || selectedTile.hasBuilding)
        {
            Log("该地块无法建造（已有建筑/无效地块）");
            return;
        }

        // 存储选中的地块
        _selectedTile = selectedTile;
        // 显示弹窗
        gameObject.SetActive(true); 
    }

    /// <summary>
    /// 隐藏建造弹窗
    /// </summary>
    public void HidePanel()
    {
        gameObject.SetActive(false);
        // 清空选中的地块
        _selectedTile = null;
        if (PopupRootManager.Instance != null)
        {
            if (PopupRootManager.Instance.CurrentShowPopup == PopupRootManager.CurrentPopupType.BuildSelect)
            {
                PopupRootManager.Instance.CurrentShowPopup = PopupRootManager.CurrentPopupType.None;
                Debug.Log("建造弹窗关闭，重置状态为None");
            }
            PopupRootManager.Instance.CheckAndHideBlocker();
        }
    }

    /// <summary>
    /// 绑定按钮事件
    /// </summary>
    private void BindButtonEvents()
    {
        // 建造仓库
        btnWarehouse?.onClick.AddListener(() => RequestBuild("Warehouse", warehouseFood, warehouseMat, warehouseEnergy));
        // 建造电池
        btnBattery?.onClick.AddListener(() => RequestBuild("Battery", batteryFood, batteryMat, batteryEnergy));
        // 建造食堂
        btnCanteen?.onClick.AddListener(() => RequestBuild("Canteen", canteenFood, canteenMat, canteenEnergy));
        // 取消
        btnCancel?.onClick.AddListener(HidePanel);
    }

    /// <summary>
    /// 发起建造请求（复用原有逻辑）
    /// </summary>
    private void RequestBuild(string proto, int f, int m, int en)
    {
        if (_selectedTile == null)
        {
            Log("未选中有效地块");
            return;
        }
        BuildingDef currentDef = null;
        float buildDuration = 0f;
        switch (proto)
        {
            case "Warehouse":
                currentDef = warehouseDef;
                buildDuration = warehouseDuration;
                break;
            case "Battery":
                currentDef = batteryDef;
                buildDuration = batteryDuration;
                break;
            case "Canteen":
                currentDef = canteenDef;
                buildDuration = canteenDuration;
                break;
            default:
                Log($"未知建筑原型：{proto}");
                return;
        }
        // 校验配置是否绑定
        if (currentDef == null)
        {
            Log($"[{proto}] 未绑定BuildingDef配置！");
            return;
        }
        // 读取预留的建造时长字段
        buildDuration = currentDef.buildTimeGameSeconds;
        if (buildDuration <= 0)
        {
            buildDuration = 3f; // 默认3秒
            Debug.LogWarning($"【倒计时兜底】{proto}的buildTimeGameSeconds≤0，设为默认3秒");
        }

        var tx = Guid.NewGuid();
        // 2. 发布建造请求（如果BuildRequest里有BuildDuration字段，就传入）
        _eventBus?.Publish(new BuildRequest
        {
            PrototypeId = proto,
            CellPosition = _selectedTile.cellPosition,
            FoodCost = f,
            MatCost = m,
            EnergyCost = en,
            TxId = tx,
            //  传入配置里的建造时长
            BuildDuration = buildDuration
        });

        // 3. 标记地块为建造中（补充状态）
        _selectedTile.hasBuilding = true;
        _selectedTile.buildingPrototypeId = proto;
        _selectedTile.isBuilding = true; // 需在HexTileData里加这个字段
        _selectedTile.buildRemainingTime = buildDuration; // 需在HexTileData里加这个字段
        // 提前生成实例ID并存储，避免后续协程中生成时丢失
        _selectedTile.buildingInstanceId = $"{proto}_{_selectedTile.cellPosition.x}_{_selectedTile.cellPosition.y}_{_selectedTile.cellPosition.z}";
      
        // 4. 启动建造倒计时协程
        if (_hexGrid != null)
        {
            _hexGrid.StartGlobalCoroutine(BuildCountdownCoroutine(proto, _selectedTile, currentDef));
            Debug.Log($"【全局协程】已将{proto}的建造协程移到HexGridData执行");
        }
        else
        {
            StartCoroutine(BuildCountdownCoroutine(proto, _selectedTile, currentDef));
            Debug.LogWarning($"【降级处理】HexGridData为空，使用本地协程");
        }

        // 建造后关闭弹窗
        HidePanel();
    }

    /// <summary>
    /// 生成建筑实例（复用原有逻辑）
    /// </summary>
    private void SpawnBuildingInstance(string proto, HexTileData tile)
    {
        if (tile.cellPosition == Vector3Int.zero)
        {
            Debug.LogError($"[{proto}] 格子坐标为空！");
            return;
        }

        GameObject prefab = proto switch
        {
            "Warehouse" => warehousePrefab,
            "Battery" => batteryPrefab,
            "Canteen" => canteenPrefab,
            _ => null
        };

        if (prefab == null)
        {
            Debug.LogWarning($"建筑预制体未绑定：{proto}");
            return;
        }

        // 生成建筑实例
        Vector3 worldPos = _hexGrid.GetCellCenterWorld(tile.cellPosition);
        GameObject buildingObj = Instantiate(prefab, worldPos, Quaternion.identity);
        string instanceId = tile.buildingInstanceId;
        if (string.IsNullOrEmpty(instanceId))
        {
            instanceId = $"{proto}_{tile.cellPosition.x}_{tile.cellPosition.y}_{tile.cellPosition.z}";
            tile.buildingInstanceId = instanceId; // 回写确保数据一致
        }
        buildingObj.name = instanceId;

        // 添加点击脚本
        BuildingClickTrigger clickTrigger = buildingObj.GetComponent<BuildingClickTrigger>() ?? buildingObj.AddComponent<BuildingClickTrigger>();
        clickTrigger.cellPosition = tile.cellPosition;
        clickTrigger.buildingPrototypeId = proto;
        clickTrigger.buildingInstanceId = instanceId;

        clickTrigger.enabled = true;
        Debug.Log($"ClickTrigger赋值完成 → 预制体：{proto} | InstanceId：{clickTrigger.buildingInstanceId} | 物体名：{buildingObj.name}");
        // 记录到格子数据
        tile.buildingInstance = buildingObj;
        tile.buildingInstanceId = instanceId; 
        tile.buildingPrototypeId = proto;
        tile.isBuilding = false; // 建造完成，重置建造状态
        tile.buildRemainingTime = 0; // 清空剩余时间
        Debug.Log($"存储到HexTileData：cell={tile.cellPosition} | 实例ID={instanceId} | 原型ID={proto}");
    }

    /// <summary>
    /// 日志显示
    /// </summary>
    private void Log(string line)
    {
        if (logText != null)
        {
            logText.text = line + "\n" + logText.text;
        }
        Debug.Log($"【建造弹窗】{line}");
    }

    // 防止内存泄漏
    private void OnDestroy()
    {
        btnWarehouse?.onClick.RemoveAllListeners();
        btnBattery?.onClick.RemoveAllListeners();
        btnCanteen?.onClick.RemoveAllListeners();
        btnCancel?.onClick.RemoveAllListeners();
    }
    /// <summary>
    /// 建造倒计时协程（读取配置里的时长）
    /// </summary>
    private IEnumerator BuildCountdownCoroutine(string proto, HexTileData tile, BuildingDef def)
    {
        Debug.Log($"【协程启动】建筑类型：{proto} | 地块坐标：{tile.cellPosition} | 初始倒计时：{tile.buildRemainingTime} | isBuilding：{tile.isBuilding}");

        // 防护：如果地块数据为空，直接终止协程
        if (tile == null)
        {
            Debug.LogError($"【协程终止】地块数据为空，proto={proto}");
            yield break;
        }

        // 防护：如果倒计时≤0，直接执行建造（避免循环不执行）
        if (tile.buildRemainingTime <= 0)
        {
            Debug.LogWarning($"【协程异常】倒计时≤0，直接建造：{proto} | 剩余时间：{tile.buildRemainingTime}");
            SpawnBuildingInstance(proto, tile);
            tile.isBuilding = false;
            tile.buildRemainingTime = 0;
            yield break;
        }

        float lastPrintTime = tile.buildRemainingTime;
        // 倒计时循环（优化日志：只在秒数变化时打印）
        while (tile.buildRemainingTime > 0 && tile.isBuilding)
        {
            tile.buildRemainingTime -= Time.deltaTime;

            // 只在剩余秒数整数部分变化时打印（比如从5.9→5.0时打印一次）
            float currentIntTime = Mathf.Floor(tile.buildRemainingTime);
            float lastIntTime = Mathf.Floor(lastPrintTime);
            if (currentIntTime != lastIntTime)
            {
                Debug.Log($"【倒计时】{proto} | 剩余：{tile.buildRemainingTime:F1}秒");
                lastPrintTime = tile.buildRemainingTime;
            }

            yield return null;
        }

        // 日志2：循环结束，准备生成建筑
        Debug.Log($"【倒计时结束】{proto} | 剩余时间：{tile.buildRemainingTime} | isBuilding：{tile.isBuilding}");

        // 校验：只有建造状态为true且剩余时间≤0时，才生成建筑
        if (tile.isBuilding && tile.buildRemainingTime <= 0)
        {
            Debug.Log($"【执行生成】开始调用SpawnBuildingInstance：{proto}");
            SpawnBuildingInstance(proto, tile);
            tile.isBuilding = false; // 确保状态重置
            tile.buildRemainingTime = 0;
            Log($"[{proto}] 建造完成！生成在 {tile.cellPosition}");
        }
        else
        {
            Debug.LogError($"【生成失败】条件不满足 → isBuilding：{tile.isBuilding} | 剩余时间：{tile.buildRemainingTime}");
            tile.isBuilding = false;
            tile.buildRemainingTime = 0;
        }
    }

    /// <summary>
    /// 应用建筑完成后的容量加成
    /// </summary>
    private void ApplyBuildingCapAddition(BuildingDef def)
    {
        // 这里调用资源系统的接口，增加对应容量（比如：
        // ResourceSystem.Instance.AddFoodCap(def.addFoodCap);
        // ResourceSystem.Instance.AddMatCap(def.addMatCap);
        // ResourceSystem.Instance.AddEnergyCap(def.addEnergyCap);
        if (def.addFoodCap > 0 || def.addMatCap > 0 || def.addEnergyCap > 0)
        {
            Log($"应用建筑容量加成：食物+{def.addFoodCap} | 材料+{def.addMatCap} | 能量+{def.addEnergyCap}");
        }
    }
}