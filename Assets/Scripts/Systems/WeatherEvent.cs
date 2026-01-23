using ProjectSulamith.Core;

// 天气状态枚举（严格对应策划案节点）
public enum WeatherState
{
    Normal,          // 正常天气（默认）
    SmallStorm,      // 小型风暴
    GiantStorm,      // 巨型风暴
    StormEye,        // 风暴眼（短暂平静）
    StormEnded       // 风暴散去（结局）
}

// 天气状态变更事件（你唯一需要广播的事件）
public struct WeatherStateChangedEvent
{
    public WeatherState NewWeather;  // 当前天气状态
    public int CurrentDay;           // 触发天气的游戏天数
    public string WeatherDesc;       // 可选：天气描述（供UI/日志使用）
}