using UnityEngine;
using ProjectSulamith.Core;

/// <summary>
/// 天气广播器（天数绑定天气节点 + 事件广播）
/// </summary>
public class WeatherBroadcaster : MonoBehaviour
{
    public static WeatherBroadcaster Instance { get; private set; }

    // 当前天气状态（供外部查询）
    public WeatherState CurrentWeather { get; private set; } = WeatherState.Normal;

    // 固定天气节点（可在Inspector面板调整，方便修改）
    [Header("天气节点配置")]
    [Tooltip("小型风暴:开始(天，时)")]
    public (int day,int hour) SmallStormStart = (9,0);
    [Tooltip("小型风暴:结束(天，时)")]
    public (int day, int hour) SmallStormEnd = (10,0);
    [Tooltip("巨型风暴:开始(天，时)")]
    public (int day, int hour) GiantStormStart = (23,5);
    [Tooltip("风暴眼出现天数（固定28天）")]
    public (int day, int hour) StormEye = (28,0);
    [Tooltip("风暴散去天数（结局）")]
    public (int day, int hour) StormEnd = (30,0);

    // 内部缓存：上一次触发天气判断的时间
    private int _lastCheckTotalHour = -1;
    private void Awake()
    {
        // 单例模式（和TimeManager保持一致）
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 初始化缓存的天数
        if (TimeManager.Instance != null)
        {
            CheckWeatherbytime();
        }
        else
        {
            Debug.LogError("[WeatherBroadcaster] TimeManager实例不存在！请确保TimeManager已挂载");
        }
    }

    private void Update()
    {
        // 每帧检测：TimeManager是否存在 + 天数是否变化
        if (TimeManager.Instance == null) return;

        int currentTotalMin = TimeManager.Instance.CurrentDay * 24*60 + TimeManager.Instance.CurrentHour*60+ TimeManager.Instance.CurrentMinute;
        // 分钟数变化时，检测天气
        if (currentTotalMin != _lastCheckTotalHour)
        {
            _lastCheckTotalHour = currentTotalMin;
            CheckWeatherbytime();
        }
    }

    /// <summary>
    /// 核心逻辑：每天跨天时，判断是否触发天气节点并广播
    /// </summary>
    private void CheckWeatherbytime(bool forceBroadcast = false)
    {
        WeatherState targetWeather = CurrentWeather;
        string weatherDesc = string.Empty;
        // 把天+小时转成总小时数，方便比较
        int currentDay = TimeManager.Instance.CurrentDay;
        int currentHour = TimeManager.Instance.CurrentHour;
        int currentMin = TimeManager.Instance.CurrentMinute;
        float currentTotalHour = currentDay * 24f + currentHour+ currentMin/60f;
        float smallStormStartTotal = SmallStormStart.day * 24f + SmallStormStart.hour;
        float smallStormEndTotal = SmallStormEnd.day * 24f + SmallStormEnd.hour;
        float giantStormStartTotal = GiantStormStart.day * 24f + GiantStormStart.hour;
        float stormEyeTotal = StormEye.day * 24f + StormEye.hour;
        float stormEndTotal = StormEnd.day * 24f + StormEnd.hour;
        // 按照天数节点判断天气
        if (currentTotalHour >= smallStormStartTotal && currentTotalHour <= smallStormEndTotal)
        {
            targetWeather = WeatherState.SmallStorm;
            weatherDesc = "小型风暴来袭,请启动应急措施";
        }
        else if (currentDay > smallStormEndTotal && currentDay < giantStormStartTotal)
        {
            targetWeather = WeatherState.Normal;
            weatherDesc = "风暴暂歇";
        }
        else if (currentDay >= giantStormStartTotal && currentDay < stormEyeTotal)
        {
            targetWeather = WeatherState.GiantStorm;
            weatherDesc = "巨型风暴形成！";
        }
        else if (currentDay == stormEyeTotal)
        {
            targetWeather = WeatherState.StormEye;
            weatherDesc = "风暴眼经过，获得短暂平静";
        }
        else if (currentDay > stormEyeTotal && currentDay < stormEndTotal)
        {
            targetWeather = WeatherState.GiantStorm;
            weatherDesc = "风暴眼离去，巨型风暴再次增强";
        }
        else if (currentDay >= stormEndTotal)
        {
            targetWeather = WeatherState.StormEnded;
            weatherDesc = "巨型风暴散去，危机解除";
        }

        // 只有天气状态变化时，才广播事件（避免重复广播）
        if (targetWeather != CurrentWeather||forceBroadcast)
        {
            CurrentWeather = targetWeather;
            BroadcastWeatherChange(currentDay,currentHour, weatherDesc);
        }
    }

    /// <summary>
    /// 向所有模块发送天气变化事件
    /// </summary>
    private void BroadcastWeatherChange(int Day,int Hour, string desc)
    {
        WeatherStateChangedEvent weatherEvent = new WeatherStateChangedEvent
        {
            NewWeather = CurrentWeather,
            CurrentDay = Day,
            CurrentHour=Hour,
            WeatherDesc = desc
        };

        // 向所有模块广播天气变化
        EventBus.Instance?.Publish(weatherEvent);
        Debug.Log($"[天气广播] 第{Day}天 | {CurrentWeather} | {desc}");
    }

    // 供外部主动查询天气状态（比如UI初始化时调用）
    public WeatherState GetCurrentWeather()
    {
        return CurrentWeather;
    }
}