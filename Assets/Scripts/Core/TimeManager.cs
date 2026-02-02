/*
 * Project: ProjectSulamith
 * File: TimeManager.cs
 * Author: [Cassandra]
 * Description:
 *   全局时间控制系统。
 *   控制逻辑时间推进、模式切换、倍速控制，
 *   并通过 EventBus 向外部模块广播时间相关事件。
 */

using System;
using UnityEngine;

namespace ProjectSulamith.Core
{
    //模式
    public enum TimeMode
    {
        Simulation,  // 模拟经营模式
        Realtime,    // 实时剧情/通讯模式
        Paused,      // 暂停
        Transition   // 模式过渡中
    }

  
    public class TimeManager : MonoBehaviour
    {
        public static TimeManager Instance { get; private set; }

        [Header("时间倍率设置")]
        [Tooltip("模拟模式：逻辑时间倍率（1秒现实 = N秒游戏）")]
        [SerializeField] private float simulationBaseSpeed = 120f;

        [Tooltip("实时模式：逻辑时间倍率（通常为1）")]
        [SerializeField] private float realtimeSpeed = 1f;

        [Tooltip("模式切换的平滑过渡时长（秒）")]
        [SerializeField] private float transitionDuration = 1.0f;

        [Header("倍速控制")]
        [Tooltip("模拟模式下的自定义倍速因子")]
        [SerializeField] private float customSpeedMultiplier = 1f;
        [SerializeField] private float minMultiplier = 0.25f;
        [SerializeField] private float maxMultiplier = 4f;

        [Header("当前状态信息")]
        public TimeMode CurrentMode = TimeMode.Realtime;

      
        public double GameTimeMinutes = 0.0;

     
        public float CurrentSpeed = 0f;

   
      
        public float TargetSpeed = 0f;

        private bool _isTransitioning = false;
        private float _transitionVelocity = 0f; 
        private float _transitionTimer = 0f;    
        private float _lastBroadcastSpeed = -1f; // 节流

        
        [Header("Calendar (Derived from GameTimeMinutes)")]
        [SerializeField] private int startDay = 0;              
        [SerializeField] private float startDayTimeHour = 0f;
        [SerializeField] private float startDayTimeMinute = 0f;  

        public const int MinutesPerHour = 60;
        public const int HoursPerDay = 24;
        public const int MinutesPerDay = MinutesPerHour * HoursPerDay; 

        public int CurrentDay { get; private set; }      
        public int CurrentHour { get; private set; }    
        public int CurrentMinute { get; private set; }   

        // 节流/跨天检测缓存（不知道什么用
        private long _lastComputedDay = long.MinValue;
        private int _lastComputedHour = int.MinValue;

        #region === Unity Lifecycle ===

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            int h = Mathf.Clamp(Mathf.FloorToInt(startDayTimeHour), 0, HoursPerDay - 1);
            int m = Mathf.Clamp(Mathf.FloorToInt(startDayTimeMinute), 0, MinutesPerHour - 1);

            GameTimeMinutes = (double)startDay * MinutesPerDay + (double)h * MinutesPerHour + (double)m;

            RecomputeCalendarAndBroadcastIfNeeded(force: true);

            // 初始速率
            switch (CurrentMode)
            {
                case TimeMode.Simulation:
                    customSpeedMultiplier = Mathf.Clamp(customSpeedMultiplier, minMultiplier, maxMultiplier);
                    CurrentSpeed = simulationBaseSpeed * customSpeedMultiplier;
                    break;

                case TimeMode.Realtime:
                default:
                    customSpeedMultiplier = 1f;     
                    CurrentSpeed = realtimeSpeed;   
                    break;

                case TimeMode.Paused:
                    customSpeedMultiplier = 1f;
                    CurrentSpeed = 0f;
                    break;

                case TimeMode.Transition:
                    customSpeedMultiplier = 1f;
                    CurrentSpeed = (simulationBaseSpeed + realtimeSpeed) * 0.5f;
                    break;
            }

            TargetSpeed = CurrentSpeed;
            _lastBroadcastSpeed = CurrentSpeed;

            // 订阅
            EventBus.Instance?.Subscribe<TimeControlEvent>(OnTimeControlEvent);

            // 广播初始状态
            EventBus.Instance?.Publish(new TimeModeChangedEvent { NewMode = CurrentMode });

            EventBus.Instance?.Publish(new SpeedChangedEvent
            {
                NewSpeed = CurrentSpeed,
                CustomMultiplier = customSpeedMultiplier,
                Mode = CurrentMode
            });
        }

        private void OnDestroy()
        {
            EventBus.Instance?.Unsubscribe<TimeControlEvent>(OnTimeControlEvent);
        }

        private void Update()
        {
            float deltaReal = Time.unscaledDeltaTime;

            // 平滑速率
            if (_isTransitioning)
            {
                CurrentSpeed = Mathf.SmoothDamp(
                    CurrentSpeed,
                    TargetSpeed,
                    ref _transitionVelocity,
                    transitionDuration
                );

                _transitionTimer += deltaReal;

                if (Mathf.Abs(CurrentSpeed - TargetSpeed) <= 1f)
                {
                    _isTransitioning = false;
                    CurrentSpeed = TargetSpeed;
                }

                // 节流
                if (Mathf.Abs(CurrentSpeed - _lastBroadcastSpeed) > 0.1f)
                {
                    _lastBroadcastSpeed = CurrentSpeed;
                    EventBus.Instance?.Publish(new SpeedChangedEvent
                    {
                        NewSpeed = CurrentSpeed,
                        CustomMultiplier = customSpeedMultiplier,
                        Mode = CurrentMode
                    });
                }
            }

            
            if (CurrentMode != TimeMode.Paused)
            {
               
                double deltaGameMinutes = (double)deltaReal * (double)CurrentSpeed / 60.0;
                GameTimeMinutes += deltaGameMinutes;

                EventBus.Instance?.Publish(new GameTickEvent
                {
                    DeltaMinutes = (float)deltaGameMinutes,
                    TotalMinutes = GameTimeMinutes
                });

                RecomputeCalendarAndBroadcastIfNeeded(force: false);
            }
        }

        #endregion

        #region === 模式切换 ===

        /// <summary>
        /// 切换时间模式
        /// </summary>
        public void SetMode(TimeMode newMode)
        {
            if (newMode == CurrentMode)
            {
                Debug.Log($"[TimeManager] 模式切换被忽略：已处于 {newMode}");
                return;
            }

            CurrentMode = newMode;

            switch (newMode)
            {
                case TimeMode.Simulation:
                    TargetSpeed = simulationBaseSpeed * customSpeedMultiplier;
                    break;

                case TimeMode.Realtime:
                    TargetSpeed = realtimeSpeed;
                    customSpeedMultiplier = 1f; // 锁定
                    break;

                case TimeMode.Paused:
                    TargetSpeed = 0f;
                    break;

                case TimeMode.Transition:
                    TargetSpeed = (simulationBaseSpeed + realtimeSpeed) / 2f;
                    break;
            }

            // 开启平滑过渡
            _isTransitioning = true;
            _transitionTimer = 0f;
            _transitionVelocity = 0f;

            Debug.Log($"[TimeManager] 模式切换：{newMode} → 目标速率 {TargetSpeed:F2}");

            // 广播事件
            EventBus.Instance?.Publish(new TimeModeChangedEvent { NewMode = newMode });

            EventBus.Instance?.Publish(new SpeedChangedEvent
            {
                NewSpeed = TargetSpeed,
                CustomMultiplier = customSpeedMultiplier,
                Mode = newMode
            });
        }

        #endregion

        #region === 倍速控制（仅模拟模式） ===

        /// <summary>
        /// 设置倍速
        /// </summary>
        public void SetCustomSpeed(float multiplier)
        {
            if (CurrentMode != TimeMode.Simulation)
            {
                Debug.LogWarning("[TimeManager] 实时模式下无法调整倍速。");
                return;
            }

            customSpeedMultiplier = Mathf.Clamp(multiplier, minMultiplier, maxMultiplier);
            TargetSpeed = simulationBaseSpeed * customSpeedMultiplier;

            // 平滑插值
            _isTransitioning = true;
            _transitionTimer = 0f;
            _transitionVelocity = 0f;

            Debug.Log($"[TimeManager] 模拟倍速调整为 x{customSpeedMultiplier:F2}");

            EventBus.Instance?.Publish(new SpeedChangedEvent
            {
                NewSpeed = TargetSpeed,
                CustomMultiplier = customSpeedMultiplier,
                Mode = CurrentMode
            });
        }

        public float GetCustomSpeed() => customSpeedMultiplier;

        #endregion

        #region === 快捷方法 ===

        public void Pause() => SetMode(TimeMode.Paused);
        public void ResumeSimulation() => SetMode(TimeMode.Simulation);
        public void ResumeRealtime() => SetMode(TimeMode.Realtime);
        public void Transition() => SetMode(TimeMode.Transition);

        public float GetCurrentSpeed() => CurrentSpeed;

       
        public double GetGameMinutes() => GameTimeMinutes;

        public int GetCurrentDay() => CurrentDay;
        public int GetCurrentHour() => CurrentHour;
        public int GetCurrentMinute() => CurrentMinute;

        #endregion

        #region === 事件响应 ===

        /// <summary>
        /// 响应来自 UI 的时间控制指令事件。
        /// </summary>
        private void OnTimeControlEvent(TimeControlEvent evt)
        {
            switch (evt.Command)
            {
                case TimeControlCommand.SetRealtime:
                    SetMode(TimeMode.Realtime);
                    break;

                case TimeControlCommand.SetSimulation:
                    SetMode(TimeMode.Simulation);
                    break;

                case TimeControlCommand.Pause:
                    Pause();
                    break;

                case TimeControlCommand.SetSpeed:
                    SetCustomSpeed(evt.Multiplier);
                    break;

                default:
                    Debug.LogWarning($"[TimeManager] 未识别的时间控制命令：{evt.Command}");
                    break;
            }
        }

        #endregion

        #region === 整点事件 ===

        private void RecomputeCalendarAndBroadcastIfNeeded(bool force)
        {
           
            long totalMinutesInt = (long)System.Math.Floor(GameTimeMinutes);
            if (totalMinutesInt < 0) totalMinutesInt = 0;

            long dayIndex = totalMinutesInt / MinutesPerDay;
            long dayMinute = totalMinutesInt % MinutesPerDay; 

            int hour = (int)(dayMinute / MinutesPerHour);     
            int minute = (int)(dayMinute % MinutesPerHour);   

            CurrentDay = (int)dayIndex;
            CurrentHour = hour;
            CurrentMinute = minute;

            // 跨天/整点检测
            if (force || dayIndex != _lastComputedDay)
            {
                _lastComputedDay = dayIndex;
                
            }

            if (force || hour != _lastComputedHour)
            {
                _lastComputedHour = hour;
                
            }
        }

        #endregion
    }
}
