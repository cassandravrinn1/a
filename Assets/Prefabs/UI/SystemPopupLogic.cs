using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 系统弹窗逻辑：返回主界面/设置
/// 挂载在 SystemPopup 预制体上
/// </summary>
public class SystemPopupLogic : MonoBehaviour
{
    [Header("UI组件绑定")]
    [SerializeField] private Button _backToMainBtn; // 返回主界面按钮
    [SerializeField] private Button _settingsBtn;    // 设置按钮
    [SerializeField] private Button _cancelBtn;      // 取消按钮
    [SerializeField] private Image _background;      // 弹窗背景

    // 引用 ESC 系统
    private ESCSystem _escSystem;

    private void Start()
    {
        
    }

    private void Awake()
    {
        // 初始隐藏弹窗
        HidePanel();
        // 获取 PopupRoot 上的 ESCSystem
        _escSystem = FindObjectOfType<ESCSystem>(true);
        BindButtonEvents();
    }


    /// <summary>
    /// 绑定按钮事件
    /// </summary>
    private void BindButtonEvents()
    {
        if (_backToMainBtn != null)
        {
            _backToMainBtn.onClick.RemoveAllListeners();
            _backToMainBtn.onClick.AddListener(OnBackToMain);
        }
        if (_settingsBtn != null)
        {
            _settingsBtn.onClick.RemoveAllListeners();
            _settingsBtn.onClick.AddListener(() =>
            {
                Debug.Log("点击了设置按钮"); // 加日志验证
                OnOpenSettings();
            });
        }
        else
        {
            Debug.LogError("设置按钮未赋值！");
        }
        if (_cancelBtn != null)
        {
            _cancelBtn.onClick.RemoveAllListeners();
            _cancelBtn.onClick.AddListener(OnCancel);
        }
    }

    /// <summary>
    /// 显示弹窗
    /// </summary>
    public void ShowPanel()
    {
        gameObject.SetActive(true);
    }

    /// <summary>
    /// 隐藏弹窗
    /// </summary>
    public void HidePanel()
    {
        gameObject.SetActive(false);
    }

    // 返回主界面
    private void OnBackToMain()
    {
        _escSystem?.OnBackToMainMenu();
        HidePanel();
    }

    // 打开设置
    private void OnOpenSettings()
    {
        _escSystem?.OnOpenSettings();
        HidePanel();
    }

    // 取消
    private void OnCancel()
    {
        _escSystem?.OnCancel();
        HidePanel();
    }

    // 防止内存泄漏
    private void OnDestroy()
    {
        if (_backToMainBtn != null) _backToMainBtn.onClick.RemoveListener(OnBackToMain);
        if (_settingsBtn != null) _settingsBtn.onClick.RemoveListener(OnOpenSettings);
        if (_cancelBtn != null) _cancelBtn.onClick.RemoveListener(OnCancel);
    }
}