using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsPopupLogic : MonoBehaviour
{
    [Header("设置组件")]
    [SerializeField] private Slider _volumeSlider;
    [SerializeField] private TMP_Dropdown _graphicsDropdown;
    [SerializeField] private Button _saveBtn;
    [SerializeField] private Button _closeBtn;
    [SerializeField] private Image _background;      // 弹窗背景

    private PopupRootManager _popupManager;
    private void Awake()
    {
        HidePanel();
        _popupManager = PopupRootManager.Instance; // 获取全局管理器
    }

    private void Start()
    {
        BindButtonEvents();
        LoadSavedSettings(); // 加载上次保存的设置
    }

    private void BindButtonEvents()
    {
        _saveBtn.onClick.AddListener(SaveSettings);
        _closeBtn.onClick.AddListener(CloseSettings);
    }

    public void ShowPanel()
    {
        gameObject.SetActive(true);
        LoadSavedSettings();
    }

    public void HidePanel()
    {
        gameObject.SetActive(false);
    }

    // 加载设置（示例：PlayerPrefs 存储）
    private void LoadSavedSettings()
    {
        // 音量
        float volume = PlayerPrefs.GetFloat("MasterVolume", 0.8f);
        _volumeSlider.value = volume;

        // 画质
        int quality = PlayerPrefs.GetInt("GraphicsQuality", 2);
        _graphicsDropdown.value = quality;
    }

    // 保存设置
    private void SaveSettings()
    {
        // 音量
        float volume = _volumeSlider.value;
        PlayerPrefs.SetFloat("MasterVolume", volume);
        AudioListener.volume = volume; // 实时生效

        // 画质
        int quality = _graphicsDropdown.value;
        PlayerPrefs.SetInt("GraphicsQuality", quality);
        QualitySettings.SetQualityLevel(quality);

        Debug.Log("设置已保存");
    }

    // 关闭设置弹窗
    private void CloseSettings()
    {
        HidePanel();
        // 关闭设置后，回到系统弹窗
        _popupManager.ShowSystemPopup();
        // 同步全局状态
        _popupManager.CurrentShowPopup = PopupRootManager.CurrentPopupType.System;
    }
}