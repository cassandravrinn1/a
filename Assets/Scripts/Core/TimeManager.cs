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
using Unity.VisualScripting;
using UnityEngine;

namespace ProjectSulamith.Core
{
    /// <summary>
    /// 游戏时间模式。
    /// </summary>
    public enum TimeMode
    {
        Simulation,  // 模拟经营模式（加速时间）
        Realtime,    // 实时剧情/通讯模式
        Paused,      // 暂停（菜单或事件中）
        Transition   // 模式过渡中
    }

    /// <summary>
    /// 全局时间管理器。
    /// 控制时间流逝速率、模式切换、平滑插值与事件广播。
    /// </summary>
    public class TimeManager : MonoBehaviour
    {
        // 单例实例
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
        public TimeMode CurrentMode = TimeMode.Simulation;
        public float GameTimeMinutes = 0f;
        public float CurrentSpeed;   // 当前实际速率
        public float TargetSpeed;    // 目标速率（插值目标）

        // 内部状态控制
        private bool _isTransitioning = false;
        private float _transitionVelocity = 0f; // SmoothDamp 用的速度缓存
        private float _transitionTimer = 0f;
        private float _lastBroadcastSpeed = -1f; // 节流控制

        #region === Unity Lifecycle ===

        private void Awake()
        {
            // 确保单例唯一
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            CurrentSpeed = simulationBaseSpeed * customSpeedMultiplier;
            TargetSpeed = CurrentSpeed;

            // 订阅来自 UI 的时间控制指令事件
            EventBus.Instance?.Subscribe<TimeControlEvent>(OnTimeControlEvent);
        }

        private void OnDestroy()
        {
            EventBus.Instance?.Unsubscribe<TimeControlEvent>(OnTimeControlEvent);
        }

        private void Update()
        {
            float deltaReal = Time.unscaledDeltaTime;

            // === 平滑速率过渡 ===
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
                // 节流广播：仅当速率变化显著时广播
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

                // 当接近目标速率时停止平滑
                
            }

            // === 逻辑时间推进 ===
            if (CurrentMode != TimeMode.Paused)
            {
                float deltaGame = deltaReal * CurrentSpeed / 60f;
                GameTimeMinutes += deltaGame;

                EventBus.Instance?.Publish(new GameTickEvent
                {
                    DeltaMinutes = deltaGame,
                    TotalMinutes = GameTimeMinutes
                });
            }
        }

        #endregion

        #region === 模式切换 ===

        /// <summary>
        /// 切换时间模式（自动平滑过渡）。
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
                    customSpeedMultiplier = 1f; // 锁定倍速
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

            // 广播模式与速率变化事件
            EventBus.Instance?.Publish(new TimeModeChangedEvent
            {
                NewMode = newMode
            });

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
        /// 设置倍速（仅在模拟模式下生效）。
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

            // 开启平滑插值
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
        public float GetGameMinutes() => GameTimeMinutes;

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
    }
}
