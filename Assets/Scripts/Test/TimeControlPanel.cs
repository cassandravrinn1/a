/*
 * Project: ProjectSulamith
 * File: TimeControlPanel.cs
 * Author: [Cassandra]
 * Description:
 *   时间控制面板。
 *   通过 EventBus 发布控制事件（切换模式、暂停、调整倍速）。
 *   不再直接依赖 TimeManager.Instance。
 */

using UnityEngine;
using ProjectSulamith.Core;

namespace ProjectSulamith.UI
{
    public class TimeControlPanel : MonoBehaviour
    {
        /// <summary>
        /// 切换至实时模式。
        /// </summary>
        public void OnSetRealtime() =>
            EventBus.Instance?.Publish(new TimeControlEvent { Command = TimeControlCommand.SetRealtime });

        /// <summary>
        /// 切换至模拟模式。
        /// </summary>
        public void OnSetSimulation() =>
            EventBus.Instance?.Publish(new TimeControlEvent { Command = TimeControlCommand.SetSimulation });

        /// <summary>
        /// 暂停时间。
        /// </summary>
        public void OnPause() =>
            EventBus.Instance?.Publish(new TimeControlEvent { Command = TimeControlCommand.Pause });

        /// <summary>
        /// 倍速 0.5x。
        /// </summary>
        public void OnSpeedHalf() =>
            EventBus.Instance?.Publish(new TimeControlEvent { Command = TimeControlCommand.SetSpeed, Multiplier = 0.5f });

        /// <summary>
        /// 倍速 1x。
        /// </summary>
        public void OnSpeedNormal() =>
            EventBus.Instance?.Publish(new TimeControlEvent { Command = TimeControlCommand.SetSpeed, Multiplier = 1f });

        /// <summary>
        /// 倍速 2x。
        /// </summary>
        public void OnSpeedDouble() =>
            EventBus.Instance?.Publish(new TimeControlEvent { Command = TimeControlCommand.SetSpeed, Multiplier = 2f });
    }
}
