using System.Linq;
using UnityEngine;
using ProjectSulamith.Core; // 使用 GameTickEvent / TimeModeChangedEvent / SpeedChangedEvent

namespace ProjectSulamith.Systems
{
    // 子系统接口：愿意的话实现它即可
    public interface ISimSystem
    {
        void Initialize();
        void Tick(float deltaMinutes); // 逻辑分钟（来自 TimeManager）
        void Shutdown();
    }

    [DefaultExecutionOrder(200)]
    public class SimManager : MonoBehaviour
    {
        public static SimManager Instance { get; private set; }

        // 可以在 Inspector 手动拖拽子系统；也支持自动发现
        [SerializeField] private MonoBehaviour[] _systemBehaviours;

        private ISimSystem[] _systems;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            // 自动发现：搜集场景/子物体中实现了 ISimSystem 的组件
            var auto = GetComponentsInChildren<MonoBehaviour>(true).OfType<ISimSystem>();
            var manual = (_systemBehaviours ?? new MonoBehaviour[0]).OfType<ISimSystem>();
            _systems = manual.Concat(auto).Distinct().ToArray();

            foreach (var s in _systems) s.Initialize();
        }

        private void OnEnable()
        {
            EventBus.Instance?.Subscribe<GameTickEvent>(OnGameTick);
            EventBus.Instance?.Subscribe<TimeModeChangedEvent>(OnModeChanged);
            EventBus.Instance?.Subscribe<SpeedChangedEvent>(OnSpeedChanged);
        }

        private void OnDisable()
        {
            if (EventBus.Instance == null) return;
            EventBus.Instance.Unsubscribe<GameTickEvent>(OnGameTick);
            EventBus.Instance.Unsubscribe<TimeModeChangedEvent>(OnModeChanged);
            EventBus.Instance.Unsubscribe<SpeedChangedEvent>(OnSpeedChanged);
        }

        private void OnDestroy()
        {
            if (_systems != null) foreach (var s in _systems) s.Shutdown();
            if (Instance == this) Instance = null;
        }

        // === 核心：按“逻辑分钟”推进全部子系统（无任何放缓/加权） ===
        private void OnGameTick(GameTickEvent e)
        {
            float dt = e.DeltaMinutes;     // 由 TimeManager 决定（Realtime=1x、Simulation=设定倍率）
            foreach (var s in _systems) s.Tick(dt);
        }

        // 这些钩子保留以便将来需要（此版本不做任何额外处理）
        private void OnModeChanged(TimeModeChangedEvent e) { /* no-op */ }
        private void OnSpeedChanged(SpeedChangedEvent e) { /* no-op */ }
    }
}
