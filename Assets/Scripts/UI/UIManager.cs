
using UnityEngine;
using UnityEngine.EventSystems;
using ProjectSulamith.Core; // 里边有 TimeMode，如果没有这行就先注释掉

namespace ProjectSulamith.UI
{

    /// <summary>
    /// 全局 UI 管理器。
    /// - 负责不同 UI 图层的显隐与交互开关
    /// - 防止过渡遮罩/错误 CanvasGroup 把按钮输入吃掉
    /// - 为 TimeManager / MainMenu 提供统一入口
    /// 
    /// 用法：
    /// 1. 在场景中放一个 UIManager，挂到 UI_Root 上。
    /// 2. 在 Inspector 里把对应 CanvasGroup 拖进来。
    /// 3. 其他脚本通过 UIManager.Instance 调用 SetMode / ShowMainMenu。
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("主菜单层（仅主菜单场景用）")]
        public CanvasGroup mainMenuLayer;

        [Header("UI Panels")]
        public GameObject MainUIPanel;
        public GameObject SettingsPanel;
        public GameObject keyBindingPanel;  // 键位界面

        [Header("游戏内层")]
        public CanvasGroup simulationLayer;      // 营地/经营 UI
        public CanvasGroup communicationLayer;   // 通信/对话 UI
        public CanvasGroup overlayLayer;         // 遮罩、渐变、全屏提示


        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            // 如果需要跨场景保留 UI，可以解开：
            // DontDestroyOnLoad(gameObject);

            EnsureEventSystem();
        }

        private void Start()
        {
            ShowMainMenu();
        }
        #region Public API

        /// <summary>
        /// 主菜单场景调用：确保主菜单按钮可交互。
        /// </summary>
        public void ShowMainMenu()
        {
            SetLayer(mainMenuLayer, true);
            SetLayer(simulationLayer, false);
            SetLayer(communicationLayer, false);
            // overlay 只做特效，不应该挡住菜单点击
            if (overlayLayer != null)
            {
                overlayLayer.blocksRaycasts = false;
            }
        }

        /// <summary>
        /// 游戏场景调用：根据时间模式切 UI。
        /// TimeManager 切模式时记得调用。
        /// </summary>
        public void SetMode(TimeMode mode)
        {
            switch (mode)
            {
                case TimeMode.Simulation:
                    SetLayer(simulationLayer, true);
                    SetLayer(communicationLayer, false);
                    UnlockOverlay();
                    break;

                case TimeMode.Realtime:
                    SetLayer(simulationLayer, false);
                    SetLayer(communicationLayer, true);
                    UnlockOverlay();
                    break;

                case TimeMode.Paused:
                    // 游戏暂停但可以点菜单/弹窗，根据项目自己拓展
                    SetLayer(simulationLayer, false);
                    SetLayer(communicationLayer, false);
                    UnlockOverlay();
                    break;

                case TimeMode.Transition:
                    // 过渡时禁用底层交互，避免误点
                    SetLayer(simulationLayer, false, false);
                    SetLayer(communicationLayer, false, false);
                    LockOverlay(); // 只让过渡层吃输入
                    break;
            }
        }

        #endregion
        #region 按钮点击逻辑
        // 开始游戏按钮的功能
        public void OnStartGameClicked()
        {
            Debug.Log("开始被点击了！");
        }


        // 设置按钮的功能
        public void OnSettingsClicked()
        {
            Debug.Log("设置被点击了！");
            // 隐藏主UI，显示设置面板
            if (MainUIPanel != null)
                MainUIPanel.SetActive(false);
            if (SettingsPanel != null)
                SettingsPanel.SetActive(true);
            if (keyBindingPanel != null)
                SettingsPanel.SetActive(false);
        }

        // 返回主界面按钮
        public void OnBackToMainMenu()
        {
            Debug.Log("返回主菜单！");
            // 调用 ShowMainMenu 统一恢复主界面状态
            ShowMainMenu();
        }

        // 可选：键位绑定面板的打开逻辑（如果需要）
        public void OnKeyBindingClicked()
        {
            Debug.Log("打开键位绑定面板！");
            if (MainUIPanel != null)
                MainUIPanel.SetActive(false);
            if (SettingsPanel != null)
                SettingsPanel.SetActive(false);
            if (keyBindingPanel != null)
                SettingsPanel.SetActive(true);
        }
        #endregion

        #region Helpers


        private void SetLayer(CanvasGroup cg, bool active, bool interactable = true)
        {
            if (cg == null) return;

            cg.alpha = active ? 1f : 0f;
            cg.interactable = active && interactable;
            cg.blocksRaycasts = active && interactable;
        }

        private void LockOverlay()
        {
            if (overlayLayer == null) return;
            overlayLayer.alpha = 1f;
            overlayLayer.interactable = true;
            overlayLayer.blocksRaycasts = true;
        }

        private void UnlockOverlay()
        {
            if (overlayLayer == null) return;
            // 可见但不挡点击，按需要调
            overlayLayer.interactable = false;
            overlayLayer.blocksRaycasts = false;
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current == null)
            {
                var go = new GameObject("EventSystem");
                go.AddComponent<EventSystem>();
                go.AddComponent<StandaloneInputModule>();
            }
        }

        #endregion
    }
}
