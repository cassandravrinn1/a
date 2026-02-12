using ProjectSulamith.Core;
using ProjectSulamith.Systems;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 建筑派遣UI：点击建筑弹出，支持增减/输入人数，适配UGUI
/// </summary>
public class BuildingAssignUI : MonoBehaviour
{

    // 记录每个建筑已派遣的总人数
    private Dictionary<string, int> _buildingAssignedCount = new Dictionary<string, int>();

    [Header("UI组件绑定")]
    [SerializeField] private TMP_Text _selectedBuildingText;    // 当前选中建筑ID文本
    [SerializeField] private TMP_Text _currentCountText; // 显示当前人数
    [SerializeField] private Button _addButton;            // 人数+1按钮
    [SerializeField] private Button _minusButton;          // 人数-1按钮
    [SerializeField] private Button _confirmButton;        // 确认派遣按钮
    [SerializeField] private Button _withdrawButton;       // 撤回派遣按钮
    [SerializeField] private Button _closeButton;          // 关闭面板按钮
    [SerializeField] private TMP_Text _tipText;                // 提示文本

    [Header("配置")]
    [SerializeField] private int _minCount = 0;            // 最小派遣人数
    [SerializeField] private int _defaultBuildingMax = 3;  // 建筑上限兜底值（防止为0）
    private string _currentBuildingInstanceId;
    private int _maxAssignCount;                           // 最大可派遣人数（来自人口系统）
    private PersonAssignSystem _assignSystem;              // 派遣系统引用
    private PopulationSystem _populationSystem;            // 人口系统引用
    private ResourceSystem _resourceSystem;
    private void Awake()
    {
        // 初始隐藏面板
        HidePanel();
    }

    /// <summary>
    /// 外部调用：点击建筑后打开派遣面板（传入建筑ID）
    /// </summary>
    /// <param name="buildingInstanceId">选中的建筑实例ID</param>
    public void ShowPanel(string buildingInstanceId)
    {
        Debug.Log($"【强制日志】ShowPanel 被调用，实例ID: {buildingInstanceId}");
        // 1. 强制激活面板+清空旧数据
        gameObject.SetActive(true);
        Debug.Log(gameObject.activeSelf);
        _currentBuildingInstanceId = string.Empty;
        _maxAssignCount = _defaultBuildingMax;
        SetTip("", Color.black);

        // 2. 查找系统（加容错）
        _assignSystem = FindObjectOfType<PersonAssignSystem>(true);
        _populationSystem = FindObjectOfType<PopulationSystem>(true);
        _resourceSystem = FindObjectOfType<ResourceSystem>(true);

        // 3. 绑定按钮（必执行）
        BindButtonEvents();
        EnableAllButtons();

        // 4. 实例ID校验+强制赋值（核心修复：无ID直接兜底，有ID必赋值）
        if (string.IsNullOrEmpty(buildingInstanceId))
        {
            SetTip("建筑实例ID无效！", Color.red);
            UpdateUIDisplay("未知建筑", "无ID", 0);
            return;
        }
        // 强制赋值实例ID，解决“未选中任何建筑”
        _currentBuildingInstanceId = buildingInstanceId;

        // 5. 解析类型ID+兜底
        string prototypeId = GetPrototypeIdFromInstanceId(buildingInstanceId);
        if (string.IsNullOrEmpty(prototypeId)) prototypeId = "未知建筑";

        // 6. 读取建筑上限+兜底（兼容ResourceSystem为空）
        int buildingMaxAssign = _defaultBuildingMax;
        if (_resourceSystem != null && _resourceSystem.BuildingDefMap != null && _resourceSystem.BuildingDefMap.TryGetValue(prototypeId, out BuildingDef def))
        {
            buildingMaxAssign = def.maxAssignable;
            Debug.Log($" 成功读取 {prototypeId} 上限：{buildingMaxAssign}");
        }
        _maxAssignCount = buildingMaxAssign;

        // 7. 读取已派遣人数+兜底
        int alreadyAssigned = _buildingAssignedCount.TryGetValue(_currentBuildingInstanceId, out int count) ? count : 0;

        // 8. 人口上限校验（兼容PopulationSystem为空）
        int populationMax = _populationSystem != null ? _populationSystem.GetAssignablePopulation() : int.MaxValue;
        _maxAssignCount = Mathf.Min(_maxAssignCount, alreadyAssigned + populationMax);
        _maxAssignCount = Mathf.Max(_maxAssignCount, _minCount);

        // 9. 强制更新UI（必执行，解决不显示问题）
        UpdateUIDisplay(prototypeId, buildingInstanceId, alreadyAssigned);
        UpdateButtonStates();

        Debug.Log($"面板状态：{gameObject.activeSelf} | 实例ID：{_currentBuildingInstanceId} | 上限：{_maxAssignCount} | 已派：{alreadyAssigned}");
    }
    #region 内部辅助方法
    /// <summary>
    /// 绑定所有按钮事件（抽离成方法，简化代码）
    /// </summary>
    private void BindButtonEvents()
    {
        if (_addButton != null)
        {
            _addButton.onClick.RemoveAllListeners();
            _addButton.onClick.AddListener(OnAddCount);
            Debug.Log("+ 按钮绑定成功！");
        }
        if (_minusButton != null)
        {
            _minusButton.onClick.RemoveAllListeners();
            _minusButton.onClick.AddListener(OnMinusCount);
            Debug.Log("- 按钮绑定成功！");
        }
        if (_confirmButton != null)
        {
            _confirmButton.onClick.RemoveAllListeners();
            _confirmButton.onClick.AddListener(OnConfirmAssign);
            Debug.Log("确认按钮绑定成功！");
        }
        if (_withdrawButton != null)
        {
            _withdrawButton.onClick.RemoveAllListeners();
            _withdrawButton.onClick.AddListener(() =>
            {
                Debug.Log("点击撤回按钮，通知管理器关闭派遣弹窗");
                PopupRootManager.Instance?.HideBuildingAssignUI();
            });
            Debug.Log("撤回按钮绑定成功！");
        }
        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveAllListeners();
            _closeButton.onClick.AddListener(() =>
            {
                Debug.Log("点击关闭按钮，通知管理器关闭派遣弹窗");
                PopupRootManager.Instance?.HideBuildingAssignUI();
            });
            Debug.Log("关闭按钮绑定成功！");
        }
    }

    /// <summary>
    /// 从实例ID解析类型ID（如：Battery_0_1 → Battery）
    /// </summary>
    private string GetPrototypeIdFromInstanceId(string instanceId)
    {
        if (string.IsNullOrEmpty(instanceId)) return "";

        // 按下划线分割，取第一个部分（实例ID格式：原型名_x_y）
        string[] parts = instanceId.Split('_');
        return parts.Length > 0 ? parts[0] : "";
    }

    /// <summary>
    /// 更新UI显示内容
    /// </summary>
    private void UpdateUIDisplay(string prototypeId, string instanceId, int currentAssigned)
    {
        if (_selectedBuildingText != null)
            _selectedBuildingText.text = $"当前建筑：{prototypeId}"; // 显示类型
        if (_currentCountText != null)
            _currentCountText.text = currentAssigned.ToString();
        SetTip($"可派遣人数上限：{_maxAssignCount}", Color.black);
    }
    #endregion
    /// <summary>
    /// 隐藏派遣面板
    /// </summary>
    public void HidePanel()
    {
        // 关闭面板
        gameObject.SetActive(false);

        // 清空状态
        _currentBuildingInstanceId = string.Empty;
        if (_currentCountText != null)
            _currentCountText.text = _minCount.ToString();
        SetTip("", Color.black);
    }

    #region 按钮点击逻辑
    /// <summary>
    /// 人数+1
    /// </summary>
    private void OnAddCount()
    {
        if (!int.TryParse(_currentCountText.text, out int currentCount))
        {
            currentCount = _minCount;
        }
        currentCount = Mathf.Min(currentCount + 1, _maxAssignCount); // 不超过上限
        _currentCountText.text = currentCount.ToString();
        Debug.Log($"当前人数：{currentCount}, 文本显示：{_currentCountText.text}");

        UpdateButtonStates(); // 更新按钮状态
    }

    /// <summary>
    /// 人数-1
    /// </summary>
    private void OnMinusCount()
    {
        if (!int.TryParse(_currentCountText.text, out int currentCount))
        {
            currentCount = _minCount;
        }
        currentCount = Mathf.Max(currentCount - 1, _minCount); // 不低于下限
        _currentCountText.text = currentCount.ToString();
        Debug.Log($"当前人数：{currentCount}, 文本显示：{_currentCountText.text}");

        UpdateButtonStates(); // 更新按钮状态
    }
    /// <summary>
    /// 确认派遣：直接设置为当前显示的总人数
    /// </summary>
    private void OnConfirmAssign()
    {
        // 校验选中的实例ID
        if (string.IsNullOrEmpty(_currentBuildingInstanceId))
        {
            SetTip(" 未选中任何建筑！", Color.red);
            return;
        }

        // 校验输入人数
        if (!int.TryParse(_currentCountText.text, out int targetTotalCount) ||
            targetTotalCount < _minCount || targetTotalCount > _maxAssignCount)
        {
            SetTip($"人数需在{_minCount}-{_maxAssignCount}之间！", Color.red);
            return;
        }

        //  关键：按实例ID获取已派遣人数
        int currentTotal = _buildingAssignedCount.TryGetValue(_currentBuildingInstanceId, out int count) ? count : 0;
        int delta = targetTotalCount - currentTotal;

        bool success = false;
        if (delta > 0)
        {
            // 派遣：增加 delta 人（传入实例ID，保证独立）
            var result = _assignSystem.AssignPersonToBuilding(_currentBuildingInstanceId, delta);
            success = result.Ok;
            if (success)
            {
                _buildingAssignedCount[_currentBuildingInstanceId] = targetTotalCount;
                SetTip($"派遣成功！\n总人数：{targetTotalCount}", Color.green);
            }
            else
            {
                SetTip($" 派遣失败！\n最大可派遣：{result.MaxTotalPerson}", Color.red);
            }
        }
        else if (delta < 0)
        {
            // 撤回：减少 |delta| 人
            int withdrawCount = -delta;
            success = _assignSystem.WithdrawPersonFromBuilding(_currentBuildingInstanceId, withdrawCount);
            if (success)
            {
                _buildingAssignedCount[_currentBuildingInstanceId] = targetTotalCount;
                SetTip($" 撤回成功！\n总人数：{targetTotalCount}", Color.green);
            }
            else
            {
                SetTip($" 撤回失败！", Color.red);
            }
        }
        else
        {
            // 人数未变化
            SetTip(" 人数未变化，无需操作", Color.yellow);
            success = true;
        }

        if (success)
        {
            // 可选：关闭面板
            // HidePanel();
        }

        UpdateButtonStates();
    }
    #endregion

    #region 辅助方法
    /// <summary>
    /// 设置提示文本
    /// </summary>
    private void SetTip(string text, Color color)
    {
        if (_tipText != null)
        {
            _tipText.text = text;
            _tipText.color = color;
        }
    }

    /// <summary>
    /// 启用所有按钮
    /// </summary>
    private void EnableAllButtons()
    {
        if (_addButton != null) _addButton.interactable = true;
        if (_minusButton != null) _minusButton.interactable = true;
        if (_confirmButton != null) _confirmButton.interactable = true;
        if (_withdrawButton != null) _withdrawButton.interactable = true;
    }

    /// <summary>
    /// 禁用所有按钮
    /// </summary>
    private void DisableAllButtons()
    {
        if (_addButton != null) _addButton.interactable = false;
        if (_minusButton != null) _minusButton.interactable = false;
        if (_confirmButton != null) _confirmButton.interactable = false;
        if (_withdrawButton != null) _withdrawButton.interactable = false;
    }
    /// <summary>
    /// 动态更新按钮状态（核心优化：根据当前人数禁用/启用增减按钮）
    /// </summary>
    private void UpdateButtonStates()
    {
        if (!int.TryParse(_currentCountText.text, out int currentCount))
        {
            currentCount = _minCount;
        }

        // +1按钮：当前人数≥上限时禁用
        if (_addButton != null) _addButton.interactable = currentCount < _maxAssignCount;
        // -1按钮：当前人数≤下限是禁用
        if (_minusButton != null) _minusButton.interactable = currentCount > _minCount;
    }

    #endregion
    /*
    /// <summary>
    /// 点击空白处关闭面板
    /// </summary>
    private void Update()
    {
        // 面板没激活 → 直接返回，不执行任何检测（关键：避免全局拦截）
        if (!gameObject.activeSelf) return;

        // 只在鼠标左键按下时检测
        if (Input.GetMouseButtonDown(0))
        {
            // 先判断是否点击了UI（如果点了UI，不处理）
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            // 容错：RectTransform/Canvas为空时，直接关闭面板
            RectTransform rt = GetComponent<RectTransform>();
            Canvas parentCanvas = GetComponentInParent<Canvas>();
            if (rt == null || parentCanvas == null)
            {
                HidePanel();
                return;
            }

            // 判断是否点在面板内部
            bool isInside = RectTransformUtility.RectangleContainsScreenPoint(
                rt,
                Input.mousePosition,
                parentCanvas.worldCamera);

            // 只有点在外面才关闭
            if (gameObject.activeSelf&&!isInside)
            {
                Debug.Log("点击位置在面板外");
                HidePanel();
            }
        }
    }
    */
    /// <summary>
    /// 防止内存泄漏：移除事件监听
    /// </summary>
    private void OnDestroy()
    {
        if (_addButton != null) _addButton.onClick.RemoveListener(OnAddCount);
        if (_minusButton != null) _minusButton.onClick.RemoveListener(OnMinusCount);
        if (_confirmButton != null) _confirmButton.onClick.RemoveListener(OnConfirmAssign);
        if (_withdrawButton != null) _withdrawButton.onClick.RemoveListener(HidePanel);
        if (_closeButton != null) _closeButton.onClick.RemoveListener(HidePanel);
    }
}