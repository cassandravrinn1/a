/*
 * Project: ProjectSulamith
 * File: TimeDebugUI.cs
 * Author: [Cassandra]
 * Description:
 *   调试用时间信息显示组件。
 *   订阅 EventBus 的时间相关事件（GameTick、SpeedChanged、TimeModeChanged），
 *   实时刷新 UI 信息。
 */

using UnityEngine;
using TMPro;
using ProjectSulamith.Core;

namespace ProjectSulamith.UI
{
    /// <summary>
    /// 用于开发调试或内部测试的时间状态 UI。
    /// </summary>
    public class TimeDebugUI : MonoBehaviour
    {
        [Header("UI 绑定")]
        [Tooltip("显示时间信息的 TextMeshPro 文本组件")]
        public TMP_Text infoText;

        // 当前缓存数据
        private TimeMode _currentMode = TimeMode.Simulation;
        private float _currentSpeed = 0f;
        private float _customMultiplier = 1f;
        private float _gameMinutes = 0f;

        #region Unity Lifecycle

        private void OnEnable()
        {
            // === 订阅全局事件 ===
            if (EventBus.Instance == null)
            {
                Debug.LogWarning("[TimeDebugUI] EventBus 未初始化，无法订阅事件。");
                return;
            }

            EventBus.Instance.Subscribe<GameTickEvent>(OnGameTick);
            EventBus.Instance.Subscribe<SpeedChangedEvent>(OnSpeedChanged);
            EventBus.Instance.Subscribe<TimeModeChangedEvent>(OnModeChanged);
        }

        private void OnDisable()
        {
            if (EventBus.Instance == null) return;

            // === 取消订阅事件 ===
            EventBus.Instance.Unsubscribe<GameTickEvent>(OnGameTick);
            EventBus.Instance.Unsubscribe<SpeedChangedEvent>(OnSpeedChanged);
            EventBus.Instance.Unsubscribe<TimeModeChangedEvent>(OnModeChanged);
        }

        private void Update()
        {
            if (infoText == null) return;

            // === 实时刷新显示信息 ===
            infoText.text =
                $"模式: {_currentMode}\n" +
                $"当前速率: {_currentSpeed:F2}\n" +
                $"倍速: x{_customMultiplier:F2}\n" +
                $"游戏时间(分钟): {_gameMinutes:F1}";
        }

        #endregion

        #region 事件回调

        /// <summary>
        /// 游戏时间推进事件。
        /// </summary>
        private void OnGameTick(GameTickEvent evt)
        {
            _gameMinutes = evt.TotalMinutes;
        }

        /// <summary>
        /// 速率变化事件。
        /// </summary>
        private void OnSpeedChanged(SpeedChangedEvent evt)
        {
            _currentSpeed = evt.NewSpeed;
            _customMultiplier = evt.CustomMultiplier;
            _currentMode = evt.Mode;
        }

        /// <summary>
        /// 模式变化事件。
        /// </summary>
        private void OnModeChanged(TimeModeChangedEvent evt)
        {
            _currentMode = evt.NewMode;
        }

        #endregion
    }
}
