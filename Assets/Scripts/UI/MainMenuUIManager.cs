using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
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
}