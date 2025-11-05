/*
 * Project: ProjectSulamith
 * File: EventBus.cs
 * Author: [Cassandra]
 * Description:
 *   全局事件调度中心。
 *   提供发布 / 订阅模式的系统级通信机制，
 *   用于模块间解耦（如 TimeManager、SimManager、UIManager、InkManager）。
 */

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectSulamith.Core
{
    /// <summary>
    /// 事件总线系统。
    /// 允许模块间通过泛型事件类型进行松耦合通信。
    /// </summary>
    public class EventBus : MonoBehaviour
    {
        // 懒加载单例
        private static EventBus _instance;
        public static EventBus Instance
        {
            get
            {
                if (_instance == null)
                {
                    // 尝试查找已有实例
                    _instance = FindObjectOfType<EventBus>();
                    if (_instance == null)
                    {
                        // 若不存在则自动创建一个
                        var go = new GameObject("[EventBus]");
                        _instance = go.AddComponent<EventBus>();
                        DontDestroyOnLoad(go);
                        Debug.Log("[EventBus] 已自动创建全局实例。");
                    }
                }
                return _instance;
            }
        }

        // 存储事件类型与对应委托
        private readonly Dictionary<Type, Delegate> _eventTable = new();

        private void Awake()
        {
            // 保证单例唯一
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        #region === 发布与订阅核心逻辑 ===

        /// <summary>
        /// 订阅指定类型的事件。
        /// </summary>
        public void Subscribe<T>(Action<T> listener)
        {
            var type = typeof(T);
            if (_eventTable.TryGetValue(type, out var existingDelegate))
                _eventTable[type] = Delegate.Combine(existingDelegate, listener);
            else
                _eventTable[type] = listener;
        }

        /// <summary>
        /// 取消订阅指定类型的事件。
        /// </summary>
        public void Unsubscribe<T>(Action<T> listener)
        {
            var type = typeof(T);
            if (_eventTable.TryGetValue(type, out var existingDelegate))
            {
                var newDelegate = Delegate.Remove(existingDelegate, listener);
                if (newDelegate == null)
                    _eventTable.Remove(type);
                else
                    _eventTable[type] = newDelegate;
            }
        }

        /// <summary>
        /// 发布事件（立即触发）。
        /// </summary>
        public void Publish<T>(T evt)
        {
            if (_eventTable.TryGetValue(typeof(T), out var d))
            {
                if (d is Action<T> callback)
                    callback.Invoke(evt);
            }
        }

        #endregion
    }

    #region === 全局事件定义 ===

    /// <summary>
    /// 时间速率变化事件。
    /// 由 TimeManager 在倍速调整或模式切换时发布。
    /// </summary>
    public struct SpeedChangedEvent
    {
        public float NewSpeed;         // 当前目标速率
        public float CustomMultiplier; // 当前倍速（相对模拟基础速度）
        public TimeMode Mode;          // 当前时间模式
    }

    /// <summary>
    /// 模式切换事件。
    /// 由 TimeManager 触发，用于 UI 或系统响应。
    /// </summary>
    public struct TimeModeChangedEvent
    {
        public TimeMode NewMode;
    }

    /// <summary>
    /// 游戏逻辑时间推进事件。
    /// 通常每帧或每 Tick 触发，用于驱动系统模拟。
    /// </summary>
    public struct GameTickEvent
    {
        public float DeltaMinutes;     // 增量分钟
        public float TotalMinutes;     // 游戏内累计分钟
    }

    /// <summary>
    /// 时间控制命令类型。
    /// </summary>
    public enum TimeControlCommand
    {
        SetRealtime,
        SetSimulation,
        Pause,
        SetSpeed
    }

    /// <summary>
    /// 时间控制事件（由 UI 发布，TimeManager 监听）。
    /// </summary>
    public struct TimeControlEvent
    {
        public TimeControlCommand Command; // 命令类型
        public float Multiplier;           // 倍速（仅 SetSpeed 时使用）
    }

    #endregion
}
