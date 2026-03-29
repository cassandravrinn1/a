using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ProjectSulamith.Core;
using ProjectSulamith.Systems;

public class TestBuildPanel : MonoBehaviour
{
 

    [Header("Refs")]
    public TMP_Text resText;       // 显示三资源与上限
    public TMP_Text logText;       // 事件日志（可选）


    private EventBus _bus;

    void OnEnable()
    {
        _bus = EventBus.Instance;
        _bus?.Subscribe<ResourceChangedEvent>(OnResChanged);
        /*
        // 绑定打开派遣面板按钮
        if (btnOpenAssignPanel)
        {
            btnOpenAssignPanel.onClick.AddListener(OnOpenAssignPanel);
            btnOpenAssignPanel.interactable = false; // 初始禁用（未选中有建筑的格子）
        }*/
    }

    void OnDisable()
    {
        _bus?.Unsubscribe<ResourceChangedEvent>(OnResChanged);
        _bus = null;
    }
 
    private void OnResChanged(ResourceChangedEvent e)
    {
        if (resText)
            resText.text = $"Food {e.Food}/{e.CapFood} | Mat {e.Mat}/{e.CapMat} | Energy {e.Energy}/{e.CapEnergy}";
    }
    //资源显示

    private void Log(string line)
    {
        if (!logText) return;
        logText.text = (line + "\n" + logText.text);
    }
}
