using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ESC 系统：监听ESC按键，控制系统弹窗显示/隐藏
/// 挂载在 PopupRoot 根节点上
/// </summary>
public class ESCSystem : MonoBehaviour
{
    // 依赖弹窗管理器
    private PopupRootManager _popupManager;

    private void Awake()
    {
        // 获取 PopupRoot 上的弹窗管理器
        _popupManager = GetComponent<PopupRootManager>();
        if (_popupManager == null)
        {
            Debug.LogError("PopupRoot 上未挂载 PopupRootManager 脚本！");
        }
    }

    private void Update()
    {
        // 监听 ESC 按键（只在非输入状态下生效）
        if (Input.GetKeyDown(KeyCode.Escape) && !IsInputFieldFocused())
        {
            OnESCKeyDown();
        }
    }

    /// <summary>
    /// ESC 按键按下逻辑
    /// </summary>
    private void OnESCKeyDown()
    {
        // 根据全局弹窗状态处理
        switch (_popupManager.CurrentShowPopup)
        {
            case PopupRootManager.CurrentPopupType.None:
                // 无弹窗 → 打开系统弹窗
                _popupManager.ShowSystemPopup();
                break;

            case PopupRootManager.CurrentPopupType.System:
                // 显示系统弹窗 → 关闭
                _popupManager.HideSystemPopup();
                break;

            case PopupRootManager.CurrentPopupType.Settings:
                // 显示设置弹窗 → 关闭设置，回到系统弹窗
                _popupManager.HideSettingsPopup();
                _popupManager.ShowSystemPopup();
                break;

            case PopupRootManager.CurrentPopupType.BuildingAssign:
                // 显示建筑派遣弹窗 → 关闭
                _popupManager.HideBuildingAssignUI();
                break;
            case PopupRootManager.CurrentPopupType.BuildSelect:
                // 显示建造弹窗 → 关闭
                _popupManager.HideBuildSelectPopup();
                break;
        }
    }

  
    /// <summary>
    /// 辅助：判断是否有输入框聚焦（避免输入时按ESC触发弹窗）
    /// </summary>
    private bool IsInputFieldFocused()
    {
        return UnityEngine.EventSystems.EventSystem.current != null &&
               UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject != null &&
               UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject.GetComponent<TMPro.TMP_InputField>() != null;
    }

    // 系统弹窗回调：返回主界面
    public void OnBackToMainMenu()
    {
        // 先关闭所有弹窗，避免残留
        _popupManager.HideAllPopups();

        // 加载主菜单场景,buildsettings中索引为0
        SceneManager.LoadScene(0);
    }

    // 系统弹窗回调：打开设置
    public void OnOpenSettings()
    {
        if (PopupRootManager.Instance != null)
        {
            PopupRootManager.Instance.ShowSettingsPopup();
        }
    }

    // 系统弹窗回调：取消（关闭弹窗）
    public void OnCancel()
    {
        _popupManager.HideSystemPopup();
    }
}