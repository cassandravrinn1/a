using UnityEditor.Search;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject MainUIPanel;
    public GameObject SettingsPanel;
    public GameObject keyBindingPanel;  // 键位界面
    void Start()
    {
        ShowMainMenu();
    }
    // 开始游戏按钮的功能
    public void OnStartGameClicked()
    {
        Debug.Log("开始游戏被点击了！");
        // 加载游戏场景
        SceneManager.LoadScene("GameScene");
    }

    // 继续游戏按钮的功能
    public void OnContinueGameClicked()
    {
        Debug.Log("继续游戏被点击了！");
        // 加载存档逻辑
    }

    // 设置按钮的功能
    public void OnSettingsClicked()
    {
        Debug.Log("设置被点击了！");
        if (MainUIPanel != null)
            MainUIPanel.SetActive(false);

        if (SettingsPanel != null)
            SettingsPanel.SetActive(true);
        // 打开设置面板
    }

    // 退出游戏按钮的功能
    public void OnQuitGameClicked()
    {
        Debug.Log("退出游戏被点击了！");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
    }

    //返回主界面按钮
    public void OnBackToMainMenu()
    {
        Debug.Log("设置被点击了！");
        if (MainUIPanel != null)
            MainUIPanel.SetActive(true);
        if (keyBindingPanel != null)
            keyBindingPanel.SetActive(false);
        if (SettingsPanel != null)
            SettingsPanel.SetActive(false);
        // 打开设置面板
    }
    private void ShowMainMenu()
    {
        if (MainUIPanel != null)
            MainUIPanel.SetActive(true);
        if (keyBindingPanel != null)
            keyBindingPanel.SetActive(false);
        if (SettingsPanel != null)
            SettingsPanel.SetActive(false);
    }
}