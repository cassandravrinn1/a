using ProjectSulamith.Core;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PopupRoot 管理器：统一管理所有弹窗，和UI root并列
/// 挂载在 PopupRoot 根节点上
/// </summary>
public class PopupRootManager : MonoBehaviour
{
    // 单例：全局唯一的弹窗管理器入口
    public static PopupRootManager Instance;

    [Header("弹窗预制体配置")]
    public GameObject BuildingAssignUIPrefab;
    public GameObject SystemPopupPrefab;
    public GameObject SettingsPopupPrefab;
    public GameObject BuildSelectPopupPrefab; // 拖入建造弹窗预制体

    [Header("弹窗容器")]
    public Transform PopupContainer;
    [Header("全屏阻挡")]
    public GameObject UIBlocker;
    // 存储已创建的弹窗实例
    private Dictionary<string, MonoBehaviour> _popupInstances = new Dictionary<string, MonoBehaviour>();

    // 全局弹窗状态（记录当前显示的弹窗类型）
    public enum CurrentPopupType
    {
        None,        // 无弹窗
        System,      // 系统弹窗（ESC主弹窗）
        Settings,    // 设置弹窗
        BuildingAssign, // 建筑派遣弹窗
        BuildSelect
    }
    public CurrentPopupType CurrentShowPopup = CurrentPopupType.None;
    private void Awake()
    {
        // 单例初始化（跨场景保留）
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        if (UIBlocker != null)
        {
            UIBlocker.SetActive(false);
            Debug.Log("UIBlocker 初始化为非激活状态");
        }
        // 初始化
        InitBuildingAssignUI();
        InitSystemPopup();
        InitSettingsPopup();
        InitBuildSelectPopup();
    }

    /// <summary>
    /// 初始化建筑派遣弹窗：创建实例并挂载到 PopupContainer 下
    /// </summary>
    private void InitBuildingAssignUI()
    {
        string popupKey = "BuildingAssignUI";
        if (!_popupInstances.ContainsKey(popupKey) && BuildingAssignUIPrefab != null)
        {
            // 实例化弹窗，父节点设为 PopupContainer
            GameObject popupObj = Instantiate(BuildingAssignUIPrefab, PopupContainer);
            popupObj.name = "BuildingAssignUI"; // 重命名，方便识别

            // 获取弹窗逻辑脚本并存储
            BuildingAssignUI assignUI = popupObj.GetComponent<BuildingAssignUI>();
            if (assignUI != null)
            {
                _popupInstances.Add(popupKey, assignUI);
                Debug.Log("建筑派遣弹窗已初始化到 PopupRoot 下");
            }
            else
            {
                Debug.LogError("BuildingAssignUI 预制体缺少 BuildingAssignUI 脚本！");
                Destroy(popupObj);
            }
        }
    }
    /// <summary>
    /// 初始化系统弹窗
    /// </summary>
    private void InitSystemPopup()
    {
        string popupKey = "SystemPopup";
        if (!_popupInstances.ContainsKey(popupKey) && SystemPopupPrefab != null)
        {
            GameObject popupObj = Instantiate(SystemPopupPrefab, PopupContainer);
            popupObj.name = "SystemPopup";

            // 获取系统弹窗脚本（后续创建）
            SystemPopupLogic popupLogic = popupObj.GetComponent<SystemPopupLogic>();
            if (popupLogic != null)
            {
                _popupInstances.Add(popupKey, popupLogic);
                Debug.Log("系统弹窗已初始化到 PopupRoot 下");
            }
            else
            {
                Debug.LogError("SystemPopup 预制体缺少 SystemPopupLogic 脚本！");
                Destroy(popupObj);
            }
        }
    }
    private void InitSettingsPopup()
    {
        string popupKey = "SettingsPopup";
        if (!_popupInstances.ContainsKey(popupKey) && SettingsPopupPrefab != null)
        {
            GameObject popupObj = Instantiate(SettingsPopupPrefab, PopupContainer);
            popupObj.name = "SettingsPopup";
            SettingsPopupLogic logic = popupObj.GetComponent<SettingsPopupLogic>();
            if (logic != null)
            {
                _popupInstances.Add(popupKey, logic);
            }
        }
    }

    // 新增：初始化建造选择弹窗
    private void InitBuildSelectPopup()
    {
        string popupKey = "BuildSelectPopup";
        if (!_popupInstances.ContainsKey(popupKey) && BuildSelectPopupPrefab != null)
        {
            GameObject popupObj = Instantiate(BuildSelectPopupPrefab, PopupContainer);
            popupObj.name = "BuildSelectPopup";
            BuildSelectPopupLogic logic = popupObj.GetComponent<BuildSelectPopupLogic>();
            if (logic != null)
            {
                _popupInstances.Add(popupKey, logic);
                Debug.Log("建造选择弹窗已初始化");
            }
            else
            {
                Debug.LogError("建造弹窗缺少 BuildSelectPopupLogic 脚本！");
                Destroy(popupObj);
            }
        }
    }
    /// <summary>
    /// 统一通道：外部调用显示建筑派遣弹窗
    /// </summary>
    /// <param name="buildingInstanceId">建筑实例ID</param>
    public void ShowBuildingAssignUI(string buildingInstanceId)
    {
        HideAllPopups();
        ShowUIBlocker();
        string popupKey = "BuildingAssignUI";
        if (_popupInstances.TryGetValue(popupKey, out MonoBehaviour mono) && mono is BuildingAssignUI assignUI)
        {
            // 调用原有逻辑显示面板
            assignUI.ShowPanel(buildingInstanceId);
            CurrentShowPopup = CurrentPopupType.BuildingAssign; // 更新状态
        }
        else
        {
            Debug.LogError("建筑派遣弹窗未初始化！");
            // 容错：重新初始化
            InitBuildingAssignUI();
        }
    }

    /// <summary>
    /// 统一通道：外部调用隐藏建筑派遣弹窗
    /// </summary>
    public void HideBuildingAssignUI()
    {
        string popupKey = "BuildingAssignUI";
        if (_popupInstances.TryGetValue(popupKey, out MonoBehaviour mono) && mono is BuildingAssignUI assignUI)
        {
            assignUI.HidePanel();
            if (CurrentShowPopup == CurrentPopupType.BuildingAssign)
            {
                CurrentShowPopup = CurrentPopupType.None;
            }
        }
        CheckAndHideBlocker();
    }

    /// <summary>
    /// 获取弹窗实例（供特殊场景调用）
    /// </summary>
    public T GetPopup<T>() where T : MonoBehaviour
    {
        string popupKey = typeof(T).Name;
        if (_popupInstances.TryGetValue(popupKey, out MonoBehaviour mono) && mono is T popup)
        {
            return popup;
        }
        return null;
    }
    /// <summary>
    /// 统一通道 - 显示系统弹窗
    /// </summary>
    public void ShowSystemPopup()
    {
        HideAllPopups();
        ShowUIBlocker();
        string popupKey = "SystemPopup";
        if (_popupInstances.TryGetValue(popupKey, out MonoBehaviour mono) && mono is SystemPopupLogic systemPopup)
        {
            systemPopup.ShowPanel();
            CurrentShowPopup = CurrentPopupType.System; // 更新状态
        }
        else
        {
            Debug.LogError("系统弹窗未初始化！");
            InitSystemPopup();
        }
    }

    public void ShowSettingsPopup()
    {
        HideAllPopups();
        ShowUIBlocker();
        string popupKey = "SettingsPopup";
        if (_popupInstances.TryGetValue(popupKey, out MonoBehaviour mono) && mono is SettingsPopupLogic settings)
        {
            settings.ShowPanel();
            CurrentShowPopup = CurrentPopupType.Settings; // 更新状态
        }
    }
    // 外部调用显示建造弹窗
    public void ShowBuildSelectPopup(HexTileData selectedTile)
    {
        HideAllPopups(); // 先关闭其他弹窗
        ShowUIBlocker();
        string popupKey = "BuildSelectPopup";
        if (_popupInstances.TryGetValue(popupKey, out MonoBehaviour mono) && mono is BuildSelectPopupLogic buildPopup)
        {
            buildPopup.ShowPanel(selectedTile);
            CurrentShowPopup = CurrentPopupType.BuildSelect; // 复用该状态，或新增枚举值
        }
        else
        {
            Debug.LogError("建造选择弹窗未初始化！");
            InitBuildSelectPopup();
        }
    }
    /// <summary>
    /// 统一通道 - 隐藏系统弹窗
    /// </summary>
    public void HideSystemPopup()
    {
        string popupKey = "SystemPopup";
        if (_popupInstances.TryGetValue(popupKey, out MonoBehaviour mono) && mono is SystemPopupLogic systemPopup)
        {
            systemPopup.HidePanel();
            if (CurrentShowPopup == CurrentPopupType.System)
            {
                CurrentShowPopup = CurrentPopupType.None; // 重置状态
            }

        }
        CheckAndHideBlocker();
    }

    public void HideSettingsPopup()
    {
        string popupKey = "SettingsPopup";
        if (_popupInstances.TryGetValue(popupKey, out MonoBehaviour mono) && mono is SettingsPopupLogic settings)
        {
            settings.HidePanel();
            if (CurrentShowPopup == CurrentPopupType.Settings)
            {
                CurrentShowPopup = CurrentPopupType.None; // 重置状态
            }
        }
        CheckAndHideBlocker();
    }
    public void HideBuildSelectPopup()
    {
        string popupKey = "BuildSelectPopup";
        if (_popupInstances.TryGetValue(popupKey, out MonoBehaviour mono) && mono is BuildSelectPopupLogic buildPopup)
        {
            buildPopup.HidePanel();
            if (CurrentShowPopup == CurrentPopupType.BuildSelect)
            {
                CurrentShowPopup = CurrentPopupType.None; // 重置状态
            }
        }
        CheckAndHideBlocker();
    }
    public void HideAllPopups()
    {
        HideSystemPopup();
        HideSettingsPopup();
        HideBuildingAssignUI();
        HideBuildSelectPopup();
        CurrentShowPopup = CurrentPopupType.None;

        CheckAndHideBlocker();
    }
    /// <summary>
    /// 检查是否还有弹窗，没有则关闭阻挡层
    /// </summary>
    public void CheckAndHideBlocker()
    {
        bool anyPopupActive = false;
        foreach (var popup in _popupInstances.Values)
        {
            if (popup != null && popup.gameObject.activeSelf)
            {
                anyPopupActive = true;
                break;
            }
        }
        if (CurrentShowPopup == CurrentPopupType.None && UIBlocker != null && UIBlocker.activeSelf)
        {
            HideUIBlocker();
            Debug.Log("所有弹窗已关闭，隐藏阻挡层");
        }
        else if (CurrentShowPopup != CurrentPopupType.None && UIBlocker != null && !UIBlocker.activeSelf)
        {
            ShowUIBlocker();
            Debug.Log($"当前弹窗状态：{CurrentShowPopup}，强制激活阻挡层");
        }
    }
    public void ShowUIBlocker()
    {
        if (UIBlocker != null)
            UIBlocker.SetActive(true);
    }
    public void HideUIBlocker()
    {
        if (UIBlocker != null)
            UIBlocker.SetActive(false);
    }
}