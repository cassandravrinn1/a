using UnityEngine;
using ProjectSulamith.Core;

/// <summary>
/// 天气广播器（仅负责：天数绑定天气节点 + 事件广播）
/// 无业务逻辑，只做状态通知
/// </summary>
public class WeatherBroadcaster : MonoBehaviour
{
    public static WeatherBroadcaster Instance { get; private set; }

    // 当前天气状态（供外部查询）
    public WeatherState CurrentWeather { get; private set; } = WeatherState.Normal;

    // 策划案固定天气节点（可在Inspector面板调整，方便修改）
    [Header("天气节点配置（与天数绑定）")]
    [Tooltip("小型风暴开始天数（1-10天，建议设8-10天）")]
    [SerializeField] private int smallStormStartDay = 9;
    [Tooltip("小型风暴结束天数")]
    [SerializeField] private int smallStormEndDay = 10;
    [Tooltip("巨型风暴开始天数（21-30天）")]
    [SerializeField] private int giantStormStartDay = 21;
    [Tooltip("风暴眼出现天数（固定28天）")]
    [SerializeField] private int stormEyeDay = 28;
    [Tooltip("风暴散去天数（结局）")]
    [SerializeField] private int stormEndDay = 30;

    // 内部缓存：上一帧的天数，用于检测天数变化
    private int _lastDetectedDay = -1;
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
            _lastDetectedDay = TimeManager.Instance.CurrentDay;
            // 初始化时直接检测一次天气，避免开局天数已在风暴节点但未广播
            CheckWeatherByDay(_lastDetectedDay, forceBroadcast: true);
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

        int currentDay = TimeManager.Instance.CurrentDay;
        if (currentDay != _lastDetectedDay)
        {
            _lastDetectedDay = currentDay;
            // 天数变化时，检测并广播天气
            CheckWeatherByDay(currentDay);
        }
    }

    /// <summary>
    /// 核心逻辑：每天跨天时，判断是否触发天气节点并广播
    /// </summary>
    private void CheckWeatherByDay(int currentDay, bool forceBroadcast = false)
    {
        WeatherState targetWeather = CurrentWeather;
        string weatherDesc = string.Empty;

        // 严格按策划案的天数节点判断天气
        if (currentDay >= smallStormStartDay && currentDay <= smallStormEndDay)
        {
            targetWeather = WeatherState.SmallStorm;
            weatherDesc = "小型风暴来袭！请启动应急措施";
        }
        else if (currentDay > smallStormEndDay && currentDay < giantStormStartDay)
        {
            targetWeather = WeatherState.Normal;
            weatherDesc = "风暴暂歇，抓紧时间研究科技";
        }
        else if (currentDay >= giantStormStartDay && currentDay < stormEyeDay)
        {
            targetWeather = WeatherState.GiantStorm;
            weatherDesc = "巨型风暴形成！进入最高警戒状态";
        }
        else if (currentDay == stormEyeDay)
        {
            targetWeather = WeatherState.StormEye;
            weatherDesc = "风暴眼经过，获得短暂平静";
        }
        else if (currentDay > stormEyeDay && currentDay < stormEndDay)
        {
            targetWeather = WeatherState.GiantStorm;
            weatherDesc = "风暴眼离去，巨型风暴再次增强";
        }
        else if (currentDay >= stormEndDay)
        {
            targetWeather = WeatherState.StormEnded;
            weatherDesc = "巨型风暴散去，危机解除！";
        }

        // 只有天气状态变化时，才广播事件（避免重复广播）
        if (targetWeather != CurrentWeather)
        {
            CurrentWeather = targetWeather;
            BroadcastWeatherChange(currentDay, weatherDesc);
        }
    }

    /// <summary>
    /// 向所有模块发送天气变化事件
    /// </summary>
    private void BroadcastWeatherChange(int currentDay, string desc)
    {
        WeatherStateChangedEvent weatherEvent = new WeatherStateChangedEvent
        {
            NewWeather = CurrentWeather,
            CurrentDay = currentDay,
            WeatherDesc = desc
        };

        // 向所有模块广播天气变化
        EventBus.Instance?.Publish(weatherEvent);
        Debug.Log($"[天气广播] 第{currentDay}天 | {CurrentWeather} | {desc}");
    }

    // 可选：供外部主动查询天气状态（比如UI初始化时调用）
    public WeatherState GetCurrentWeather()
    {
        return CurrentWeather;
    }
   
}