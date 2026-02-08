using ProjectSulamith.Core;
using ProjectSulamith.Systems;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
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
    private string _currentBuildingId;                     // 当前选中的建筑ID
    private int _maxAssignCount;                           // 最大可派遣人数（来自人口系统）
    private PersonAssignSystem _assignSystem;              // 派遣系统引用
    private PopulationSystem _populationSystem;            // 人口系统引用
    public static BuildingAssignUI Instance;
    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
        // 初始隐藏面板
        HidePanel();
    }

    /// <summary>
    /// 外部调用：点击建筑后打开派遣面板（传入建筑ID）
    /// </summary>
    /// <param name="buildingId">选中的建筑原型ID</param>
    public void ShowPanel(string buildingId)
    {
        Debug.Log($"【强制日志】ShowPanel 被调用，buildingId: {buildingId}");

        _assignSystem = FindObjectOfType<PersonAssignSystem>(true);
        _populationSystem = FindObjectOfType<PopulationSystem>(true);

        // 校验系统
        if (_assignSystem == null || _populationSystem == null)
        {
            SetTip(" 核心系统未初始化！", Color.red);
            DisableAllButtons();
        }
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
            _withdrawButton.onClick.AddListener(HidePanel); // 直接绑定关闭面板
            Debug.Log("撤回按钮绑定成功！");
        }
        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveAllListeners();
            _closeButton.onClick.AddListener(HidePanel);
            Debug.Log("关闭按钮绑定成功！");
        }
        // 强制显示面板
        gameObject.SetActive(true);
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null) { canvas.enabled = false; canvas.enabled = true; }

        // 打印状态
        Debug.Log($"面板激活状态：{gameObject.activeSelf}");

        if (string.IsNullOrEmpty(buildingId))
        {
            SetTip("建筑ID无效！", Color.red);
            Debug.LogWarning("[BuildingAssignUI] 传入的建筑ID为空！");
            return;
        }

        int buildingMaxAssign = _defaultBuildingMax; // 默认兜底
        var resourceSys = FindObjectOfType<ResourceSystem>(true);

        if (resourceSys != null && resourceSys.BuildingDefMap != null)
        {
            if (resourceSys.BuildingDefMap.TryGetValue(buildingId, out BuildingDef def))
            {
                //  真正从建筑配置里拿上限
                buildingMaxAssign = def.maxAssignable;
                Debug.Log($" 成功读取 {buildingId} 上限：{buildingMaxAssign}");
            }
            else
            {
                Debug.LogWarning($" BuildingDefMap 中找不到 {buildingId}，使用默认上限 {_defaultBuildingMax}");
            }
        }
        else
        {
            Debug.LogError($" ResourceSystem 或 BuildingDefMap 未初始化！");
        }

        //  设置当前建筑的最大可派遣人数
        _currentBuildingId = buildingId;
        _maxAssignCount = buildingMaxAssign;

        // 同时不能超过人口系统允许的总量
        int populationMax = _populationSystem?.GetAssignablePopulation() ?? 0;
        int maxPossible = _buildingAssignedCount.TryGetValue(_currentBuildingId, out int already) ? already + populationMax : populationMax;
        _maxAssignCount = Mathf.Min(_maxAssignCount, maxPossible);

        _maxAssignCount = Mathf.Max(_maxAssignCount, _minCount);

        // 更新UI显示
        if (_selectedBuildingText != null) _selectedBuildingText.text = $"当前建筑：{buildingId}";
        int currentAssigned = _buildingAssignedCount.TryGetValue(_currentBuildingId, out int count) ? count : 0;
        if (_currentCountText != null) _currentCountText.text = currentAssigned.ToString();
        SetTip($"可派遣人数上限：{_maxAssignCount}", Color.black);
        // 激活面板+更新按钮状态
        gameObject.SetActive(true);
        EnableAllButtons();
        UpdateButtonStates(); // 初始化按钮状态
    }

    /// <summary>
    /// 隐藏派遣面板
    /// </summary>
    public void HidePanel()
    {
        gameObject.SetActive(false);
        _currentBuildingId = string.Empty; // 清空选中建筑
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
        // 校验选中建筑
        if (string.IsNullOrEmpty(_currentBuildingId))
        {
            SetTip("? 未选中任何建筑！", Color.red);
            return;
        }

        // 校验输入人数（最终总人数）
        if (!int.TryParse(_currentCountText.text, out int targetTotalCount) ||
            targetTotalCount < _minCount || targetTotalCount > _maxAssignCount)
        {
            SetTip($" 人数需在{_minCount}-{_maxAssignCount}之间！", Color.red);
            return;
        }

        // 获取该建筑当前已派遣的总人数
        int currentTotal = _buildingAssignedCount.TryGetValue(_currentBuildingId, out int count) ? count : 0;

        // 计算需要派遣/撤回的差值
        int delta = targetTotalCount - currentTotal;

        bool success = false;
        if (delta > 0)
        {
            // 派遣：增加 delta 人
            var result = _assignSystem.AssignPersonToBuilding(_currentBuildingId, delta);
            success = result.Ok;
            if (success)
            {
                _buildingAssignedCount[_currentBuildingId] = targetTotalCount;
                SetTip($" 派遣成功！\n总人数：{targetTotalCount}", Color.green);
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
            success = _assignSystem.WithdrawPersonFromBuilding(_currentBuildingId, withdrawCount);
            if (success)
            {
                _buildingAssignedCount[_currentBuildingId] = targetTotalCount;
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
            // 关闭面板（可选，看你需求）
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

    /// <summary>
    /// 点击空白处关闭面板
    /// </summary>
    private void Update()
    {
        // 面板没激活就不检测
        if (!gameObject.activeSelf) return;

        // 只在鼠标按下时检测一次
        if (Input.GetMouseButtonDown(0))
        {
            // 判断是否点在面板内部
            bool isInside = RectTransformUtility.RectangleContainsScreenPoint(
                GetComponent<RectTransform>(),
                Input.mousePosition,
                GetComponentInParent<Canvas>().worldCamera);

            // 只有点在外面才关闭
            if (!isInside)
            {
                HidePanel();
            }
        }
    }

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