using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsManager : MonoBehaviour
{
    [Header("UIPanels")]
    public GameObject SettingsPanel;  // 设置界面
    public GameObject keyBindingPanel;  // 键位界面
    public GameObject MainUIPanel;//主界面

    [Header("显示设置")]
    public TMP_Dropdown screenModeDropdown;  // 窗口化/全屏下拉菜单
    public TMP_Dropdown resolutionDropdown;  // 分辨率下拉菜单

    [Header("音频设置")]
    public Slider masterVolumeSlider;        // 总音量滑块
    public Slider musicVolumeSlider;         // 音乐音量滑块  
    public Slider soundEffectsSlider;        // 音效音量滑块
    public TextMeshProUGUI masterVolumeText; // 总音量百分比文字
    public TextMeshProUGUI musicVolumeText;  // 音乐音量百分比文字
    public TextMeshProUGUI soundEffectsText; // 音效音量百分比文字

    [Header("按钮")]
    public Button keyBindingButton;          // 键位按钮
    public Button backToSettingsButton;      // 返回设置界面
    public Button applyButton;               // 应用按钮
    public Button resetButton;               // 重置按钮

    // 存储设置数据
    private SettingsData currentSettings;

    [System.Serializable]
    public class SettingsData
    {
        public int screenMode;           // 0=窗口化, 1=全屏
        public int resolutionIndex;      // 分辨率索引
        public float masterVolume = 1f;  // 总音量
        public float musicVolume = 1f;   // 音乐音量
        public float soundVolume = 1f;   // 音效音量
    }

    void Start()
    {
        // 加载保存的设置
        LoadSettings();

        // 初始化UI事件
        InitializeUIEvents();

        // 初始化分辨率选项
        InitializeResolutionOptions();

    }

    // 初始化分辨率选项
    private void InitializeResolutionOptions()
    {
        if (resolutionDropdown != null)
        {
            resolutionDropdown.ClearOptions();

            // 添加常见分辨率
            Resolution[] resolutions = Screen.resolutions;
            foreach (Resolution res in resolutions)
            {
                // 只添加16:9的常见分辨率避免列表太长
                if (res.width == 1920 && res.height == 1080 ||
                    res.width == 1366 && res.height == 768 ||
                    res.width == 1280 && res.height == 720 ||
                    res.width == 2560 && res.height == 1440)
                {
                    TMP_Dropdown.OptionData option = new TMP_Dropdown.OptionData($"{res.width} x {res.height}");
                    resolutionDropdown.options.Add(option);
                }
            }

            // 设置当前分辨率
            resolutionDropdown.value = currentSettings.resolutionIndex;
            resolutionDropdown.RefreshShownValue();
        }
    }

    // 初始化UI事件
    private void InitializeUIEvents()
    {
        // 显示设置事件
        if (screenModeDropdown != null)
            screenModeDropdown.onValueChanged.AddListener(OnScreenModeChanged);

        if (resolutionDropdown != null)
            resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);

        // 音频设置事件
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
            UpdateVolumeText(masterVolumeText, masterVolumeSlider.value);
        }

        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
            UpdateVolumeText(musicVolumeText, musicVolumeSlider.value);
        }

        if (soundEffectsSlider != null)
        {
            soundEffectsSlider.onValueChanged.AddListener(OnSoundEffectsChanged);
            UpdateVolumeText(soundEffectsText, soundEffectsSlider.value);
        }

        // 按钮事件
        if (keyBindingButton != null)
            keyBindingButton.onClick.AddListener(OnkeyBindingClicked);

        if (resetButton != null)
            resetButton.onClick.AddListener(OnResetClicked);

        if (applyButton != null)
            applyButton.onClick.AddListener(OnApplyClicked);

        if (backToSettingsButton != null)
            backToSettingsButton.onClick.AddListener(OnbackToSettingsClicked);
    }

    // 显示设置相关方法

    private void OnkeyBindingClicked()
    {
        Debug.Log("打开键位设置");
        ShowkeyBindingPanel();

    }
    private void OnbackToSettingsClicked()
    {
        Debug.Log("打开主设置界面");
        ShowSettingsPanel();

    }
    private void OnScreenModeChanged(int mode)
    {
        currentSettings.screenMode = mode;
        Debug.Log($"屏幕模式改为: {(mode == 0 ? "窗口化" : "全屏")}");
    }

    private void OnResolutionChanged(int index)
    {
        currentSettings.resolutionIndex = index;
        Debug.Log($"分辨率改为: {resolutionDropdown.options[index].text}");
    }

    // 音频设置相关方法  
    private void OnMasterVolumeChanged(float volume)
    {
        currentSettings.masterVolume = volume;
        UpdateVolumeText(masterVolumeText, volume);
        AudioListener.volume = volume; // 实际控制总音量
        Debug.Log($"总音量: {volume * 100}%");
    }
    //界面切换方法
    private void ShowSettingsPanel()
    {
        if (SettingsPanel != null)
            SettingsPanel.SetActive(true);
        if (keyBindingPanel != null)
            keyBindingPanel.SetActive(false);
        if (MainUIPanel != null)
            MainUIPanel.SetActive(false);
    }
    private void ShowkeyBindingPanel()
    {
        if (SettingsPanel != null)
            SettingsPanel.SetActive(false);
        if (keyBindingPanel != null)
            keyBindingPanel.SetActive(true);
        if (MainUIPanel != null)
            MainUIPanel.SetActive(false);
        if (KeyBindingManager.Instance.HasCustomBindings())
        {
            Debug.Log("检测到自定义键位设置");
        }
    }
    private void OnMusicVolumeChanged(float volume)
    {
        currentSettings.musicVolume = volume;
        UpdateVolumeText(musicVolumeText, volume);
        // 这里可以控制背景音乐音量
        Debug.Log($"音乐音量: {volume * 100}%");
    }

    private void OnSoundEffectsChanged(float volume)
    {
        currentSettings.soundVolume = volume;
        UpdateVolumeText(soundEffectsText, volume);
        // 这里可以控制音效音量
        Debug.Log($"音效音量: {volume * 100}%");
    }

    // 更新音量百分比文字
    private void UpdateVolumeText(TextMeshProUGUI text, float volume)
    {
        if (text != null)
            text.text = $"{Mathf.RoundToInt(volume * 100)}%";
    }

    // 按钮点击方法
    private void OnApplyClicked()
    {
        // 应用显示设置
        ApplyDisplaySettings();

        // 保存设置
        SaveSettings();

        Debug.Log("设置已应用并保存");
    }

    private void OnResetClicked()
    {
        // 重置为默认设置
        currentSettings = new SettingsData();

        // 更新UI显示
        UpdateUIFromSettings();

        Debug.Log("设置已重置为默认值");
    }

    // 应用显示设置
    private void ApplyDisplaySettings()
    {
        // 设置屏幕模式
        bool fullscreen = currentSettings.screenMode == 1;

        // 设置分辨率（简化版，实际需要更复杂的逻辑）
        if (resolutionDropdown != null && resolutionDropdown.options.Count > currentSettings.resolutionIndex)
        {
            // 这里需要根据选择的索引设置实际分辨率
            // 简化处理：只切换全屏/窗口
            Screen.fullScreenMode = fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        }
    }

    // 从设置数据更新UI
    private void UpdateUIFromSettings()
    {
        // 显示设置
        if (screenModeDropdown != null)
            screenModeDropdown.value = currentSettings.screenMode;

        if (resolutionDropdown != null)
            resolutionDropdown.value = currentSettings.resolutionIndex;

        // 音频设置
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.value = currentSettings.masterVolume;
            UpdateVolumeText(masterVolumeText, currentSettings.masterVolume);
        }

        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.value = currentSettings.musicVolume;
            UpdateVolumeText(musicVolumeText, currentSettings.musicVolume);
        }

        if (soundEffectsSlider != null)
        {
            soundEffectsSlider.value = currentSettings.soundVolume;
            UpdateVolumeText(soundEffectsText, currentSettings.soundVolume);
        }
    }

    // 保存设置
    private void SaveSettings()
    {
        string json = JsonUtility.ToJson(currentSettings);
        PlayerPrefs.SetString("GameSettings", json);
        PlayerPrefs.Save();
    }

    // 加载设置
    private void LoadSettings()
    {
        if (PlayerPrefs.HasKey("GameSettings"))
        {
            string json = PlayerPrefs.GetString("GameSettings");
            currentSettings = JsonUtility.FromJson<SettingsData>(json);
        }
        else
        {
            currentSettings = new SettingsData();
        }

        // 更新UI
        UpdateUIFromSettings();
    }
   
}