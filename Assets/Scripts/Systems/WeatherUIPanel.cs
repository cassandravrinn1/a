using UnityEngine;
using UnityEngine.UI;
using ProjectSulamith.Core;
using TMPro;

public class WeatherUIPanel : MonoBehaviour
{
    [Header("UI元素")]
    //[SerializeField] private Image weatherIcon;
    [SerializeField] private TMP_Text weatherNameText;
    [SerializeField] private TMP_Text weatherDescText;
    
    /*图标待定
    [Header("天气图标配置")]
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite smallStormSprite;
    [SerializeField] private Sprite giantStormSprite;
    [SerializeField] private Sprite stormEyeSprite;
    */
    private void OnEnable()
    {
        EventBus.Instance?.Subscribe<WeatherStateChangedEvent>(OnWeatherChanged);
        // 初始化显示当前天气
        if (WeatherBroadcaster.Instance != null)
        {
            UpdateUI(WeatherBroadcaster.Instance.CurrentWeather);
            weatherDescText.text = GetDefaultDesc(WeatherBroadcaster.Instance.CurrentWeather);
        }
        else
        {
            weatherNameText.text = "晴朗";
            weatherDescText.text = "天气平静";
        }
    }

    private void OnDisable()
    {
        EventBus.Instance?.Unsubscribe<WeatherStateChangedEvent>(OnWeatherChanged);
    }
    private void OnWeatherChanged(WeatherStateChangedEvent evt)
    {
        UpdateUI(evt.NewWeather);
        weatherDescText.text = evt.WeatherDesc;
    }
    private void UpdateUI(WeatherState state)
    {
        switch (state)
        {
            case WeatherState.Normal:
                //weatherIcon.sprite = normalSprite;
                weatherNameText.text = "晴朗";
                break;
            case WeatherState.SmallStorm:
                //weatherIcon.sprite = smallStormSprite;
                weatherNameText.text = "小型风暴";
                break;
            case WeatherState.GiantStorm:
                //weatherIcon.sprite = giantStormSprite;
                weatherNameText.text = "巨型风暴";
                break;
            case WeatherState.StormEye:
                //weatherIcon.sprite = stormEyeSprite;
                weatherNameText.text = "风暴眼";
                break;
            case WeatherState.StormEnded:
                //weatherIcon.sprite = normalSprite;
                weatherNameText.text = "风暴散去";
                break;
        }
    }
    private string GetDefaultDesc(WeatherState state)
    {
        switch (state)
        {
            case WeatherState.Normal:
                return "天气平静";
            case WeatherState.SmallStorm:
                return "小型风暴来袭！请启动应急措施";
            case WeatherState.GiantStorm:
                return "巨型风暴形成！进入最高警戒状态";
            case WeatherState.StormEye:
                return "风暴眼经过，获得短暂平静";
            case WeatherState.StormEnded:
                return "巨型风暴散去，危机解除！";
            default: // Normal
                return "天气平静，抓紧时间发展";
        }
    }
}