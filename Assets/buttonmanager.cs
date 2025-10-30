using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    [Header("按钮引用")]
    public Button startButton;
    public Button continueButton;
    public Button settingsButton;
    public Button exitButton;

    [Header("设置面板")]
    public GameObject settingsPanel;

    void Start()
    {
        startButton.onClick.AddListener(OnStartClicked);
        continueButton.onClick.AddListener(OnContinueClicked);
        settingsButton.onClick.AddListener(OnSettingsClicked);
        exitButton.onClick.AddListener(OnExitClicked);

        // 初始化UI状态
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        Debug.Log("主菜单初始化完成");
    }

    // 开始游戏按钮
    void OnStartClicked()
    {
        Debug.Log("开始新游戏");

        // 这里可以添加开始游戏的逻辑，比如：
        // 1. 加载游戏场景
        SceneManager.LoadScene("GameScene");

        // 2. 或者显示角色选择界面
        // ShowCharacterSelection();

        // 3. 或者播放过渡动画
        // StartCoroutine(StartGameTransition());
    }

    // 继续游戏按钮
    void OnContinueClicked()
    {
        Debug.Log("继续游戏");

        if (PlayerPrefs.HasKey("GameSaved"))
        {
            // 加载存档数据
            LoadGameData();
            SceneManager.LoadScene("GameScene");
        }
        else
        {
            // 没有存档，提示玩家
            Debug.Log("没有找到存档文件");
            // 可以显示一个提示框
            ShowNoSaveDataPopup();
        }
    }

    // 设置按钮
    void OnSettingsClicked()
    {
        Debug.Log("打开设置");

        // 显示/隐藏设置面板
        if (settingsPanel != null)
        {
            bool isActive = settingsPanel.activeSelf;
            settingsPanel.SetActive(!isActive);
        }

        // 或者直接打开设置场景
        // SceneManager.LoadScene("SettingsScene", LoadSceneMode.Additive);
    }

    // 退出游戏按钮
    void OnExitClicked()
    {
        Debug.Log("退出游戏");

        // 显示确认对话框
        ShowExitConfirmation();

        // 或者直接退出
        // QuitGame();
    }


    void LoadGameData()
    {
        // 这里实现加载存档的逻辑
        int level = PlayerPrefs.GetInt("CurrentLevel", 1);
        float volume = PlayerPrefs.GetFloat("MasterVolume", 1.0f);

        Debug.Log($"加载存档 - 关卡: {level}, 音量: {volume}");

        // 应用加载的数据
        AudioListener.volume = volume;
    }

    void ShowNoSaveDataPopup()
    {
        // 这里可以显示一个UI提示
        Debug.LogWarning("没有找到存档数据，请开始新游戏");

        // 简单示例：在屏幕上显示提示文字
        GameObject popup = new GameObject("NoSavePopup");
        popup.transform.SetParent(transform);

        Text text = popup.AddComponent<Text>();
        text.text = "没有找到存档文件！";
        text.color = Color.red;
        text.fontSize = 24;
        text.alignment = TextAnchor.MiddleCenter;

        // 3秒后自动销毁
        Destroy(popup, 3f);
    }

    void ShowExitConfirmation()
    {
        // 这里可以显示退出确认对话框

#if UNITY_EDITOR
        // 在编辑器中
        if (UnityEditor.EditorUtility.DisplayDialog("退出游戏", "确定要退出游戏吗？", "确定", "取消"))
        {
            QuitGame();
        }
#else
        // 在打包后的游戏中
        // 你可以自己创建一个确认对话框UI
        CreateExitConfirmationUI();
#endif
    }

    void QuitGame()
    {
        Debug.Log("游戏退出");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void CreateExitConfirmationUI()
    {
        // 创建退出确认UI
        GameObject confirmationPanel = new GameObject("ExitConfirmation");
        confirmationPanel.transform.SetParent(transform);

        // 添加背景
        Image bg = confirmationPanel.AddComponent<Image>();
        bg.color = new Color(0, 0, 0, 0.8f);

        RectTransform rt = confirmationPanel.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // 添加确认文本
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(confirmationPanel.transform);
        Text text = textObj.AddComponent<Text>();
        text.text = "确定要退出游戏吗？";
        text.color = Color.white;
        text.fontSize = 30;
        text.alignment = TextAnchor.MiddleCenter;



        RectTransform textRt = textObj.GetComponent<RectTransform>();
        textRt.anchorMin = new Vector2(0.5f, 0.6f);
        textRt.anchorMax = new Vector2(0.5f, 0.6f);
        textRt.sizeDelta = new Vector2(400, 50);

        // 添加确认按钮
        GameObject confirmBtn = CreateButton("确认退出", new Vector2(0, -50), confirmationPanel.transform);
        confirmBtn.GetComponent<Button>().onClick.AddListener(QuitGame);

        // 添加取消按钮
        GameObject cancelBtn = CreateButton("取消", new Vector2(0, -120), confirmationPanel.transform);
        cancelBtn.GetComponent<Button>().onClick.AddListener(() => Destroy(confirmationPanel));
    }

    GameObject CreateButton(string buttonText, Vector2 position, Transform parent)
    {
        GameObject buttonObj = new GameObject(buttonText + "Button");
        buttonObj.transform.SetParent(parent);

        Image image = buttonObj.AddComponent<Image>();
        image.color = Color.gray;

        Button button = buttonObj.AddComponent<Button>();

        RectTransform rt = buttonObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = new Vector2(150, 50);

        // 添加文本
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform);
        Text text = textObj.AddComponent<Text>();
        text.text = buttonText;
        text.color = Color.white;
        text.fontSize = 20;
        text.alignment = TextAnchor.MiddleCenter;

        RectTransform textRt = textObj.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        return buttonObj;
    }
}