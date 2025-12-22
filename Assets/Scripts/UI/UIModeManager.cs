using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ProjectSulamith.Core;

public enum UIMode { Chat, Sim, Tech, Inventory, Settings }

public class UIModeManager : MonoBehaviour
{
    [Serializable]
    public class ModeEntry
    {
        public UIMode mode;

        [Tooltip("该模式对应的 UI 根 CanvasGroup")]
        public CanvasGroup root;

        [Header("Time")]
        [Tooltip("切到该模式时要发的 TimeControlCommand（可不配；不配则不发）")]
        public bool publishTimeCommand = true;

        public TimeControlCommand timeCommand = TimeControlCommand.SetRealtime;

        [Header("Map")]
        [Tooltip("该模式下地图是否显示")]
        public bool mapVisible = true;

        [Tooltip("该模式下地图是否可交互（会调用 mapInput.SetInputEnabled）")]
        public bool mapInputEnabled = false;
    }

    [Header("Modes (Config Table)")]
    [Tooltip("把每个模式的 CanvasGroup root 拖进来。新增界面只需要加一条。")]
    public List<ModeEntry> modes = new List<ModeEntry>();

    [Header("Default Mode")]
    public UIMode startMode = UIMode.Chat;

    [Header("Fade")]
    public float fadeTime = 0.2f;

    [Header("Map Refs")]
    [Tooltip("地图根对象（Tilemap/Overlay/Collider 等都放这里下面）")]
    public GameObject mapRoot;

    [Tooltip("地图输入接收者（保留你原接口 SetInputEnabled）")]
    public HexGridInputController mapInput;

    public UIMode Current { get; private set; }

    Coroutine _co;

    // 运行时索引，加速查找
    readonly Dictionary<UIMode, ModeEntry> _index = new Dictionary<UIMode, ModeEntry>();

    void Awake()
    {
        RebuildIndex();
    }

    void Start()
    {
        // 启动时应用默认模式（不走协程）
        ApplyImmediate(startMode);

        // 保持你原逻辑：启动时默认通讯模式并切实时
        // 这里已由 ModeEntry.timeCommand 控制，不再写死
        PublishTimeIfNeeded(startMode);
    }

    /// <summary>
    /// 兼容你原来的按钮接口：不需要改绑定
    /// </summary>
    public void SwitchToChat() => Switch(UIMode.Chat);
    public void SwitchToSim() => Switch(UIMode.Sim);

    /// <summary>
    /// 新增模式时，你可以继续加类似的便捷方法（可选）
    /// </summary>
    public void SwitchToTech() => Switch(UIMode.Tech);
    public void SwitchToInventory() => Switch(UIMode.Inventory);
    public void SwitchToSettings() => Switch(UIMode.Settings);

    public void Switch(UIMode mode)
    {
        if (mode == Current) return;

        // 找不到配置就拒绝切换（避免空引用）
        if (!_index.TryGetValue(mode, out var to) || to.root == null)
        {
            Debug.LogError($"[UIModeManager] Mode '{mode}' not configured (or root is null).");
            return;
        }

        var fromMode = Current;
        Current = mode;

        // 先立刻更新地图显示与输入（不要等 fade 结束）
        ApplyMapAndInputState(mode);

        // 再开始 UI fade 切换
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(CoSwitch(fromMode, mode));

        // 统一由模式表决定是否发 TimeControlEvent
        PublishTimeIfNeeded(mode);
    }

    IEnumerator CoSwitch(UIMode from, UIMode to)
    {
        CanvasGroup fromRoot = GetRoot(from);
        CanvasGroup toRoot = GetRoot(to);

        // 如果 fromRoot/toRoot 为空，做最小兜底：只确保 toRoot 可见可交互
        if (toRoot == null)
        {
            yield break;
        }

        // 先把目标 root 打开交互（避免 fade 过程 UI 不响应）
        SetInteractable(toRoot, true);

        // from -> fade out
        if (fromRoot != null && fromRoot != toRoot)
        {
            yield return Fade(fromRoot, fromRoot.alpha, 0f);
            SetInteractable(fromRoot, false);
        }

        // to -> fade in
        yield return Fade(toRoot, toRoot.alpha, 1f);
        SetInteractable(toRoot, true);

        // 保险：其它模式全部关掉
        DisableAllExcept(to);
    }

    void ApplyImmediate(UIMode mode)
    {
        RebuildIndex(); // 防止你在 Inspector 改了列表但没重进场景

        // 先全部关掉
        foreach (var kv in _index)
        {
            if (kv.Value.root == null) continue;
            SetAlpha(kv.Value.root, 0f);
            SetInteractable(kv.Value.root, false);
        }

        // 再打开目标
        if (_index.TryGetValue(mode, out var entry) && entry.root != null)
        {
            SetAlpha(entry.root, 1f);
            SetInteractable(entry.root, true);
        }

        Current = mode;

        // 同步地图显示与输入
        ApplyMapAndInputState(mode);
    }

    void DisableAllExcept(UIMode keep)
    {
        foreach (var kv in _index)
        {
            if (kv.Key == keep) continue;
            if (kv.Value.root == null) continue;

            // 注意：这里不 SetActive，只用 CanvasGroup 阻止吃输入
            SetAlpha(kv.Value.root, 0f);
            SetInteractable(kv.Value.root, false);
        }
    }

    void ApplyMapAndInputState(UIMode mode)
    {
        if (!_index.TryGetValue(mode, out var entry)) return;

        // 1) 地图显示/隐藏
        if (mapRoot != null)
        {
            bool wantVisible = entry.mapVisible;
            if (mapRoot.activeSelf != wantVisible)
                mapRoot.SetActive(wantVisible);
        }

        // 2) 地图输入开关（推荐所有模式都明确配置）
        if (mapInput != null)
        {
            mapInput.SetInputEnabled(entry.mapInputEnabled);
        }
    }

    void PublishTimeIfNeeded(UIMode mode)
    {
        if (!_index.TryGetValue(mode, out var entry)) return;
        if (!entry.publishTimeCommand) return;

        EventBus.Instance?.Publish(new TimeControlEvent
        {
            Command = entry.timeCommand
        });
    }

    CanvasGroup GetRoot(UIMode mode)
    {
        return _index.TryGetValue(mode, out var entry) ? entry.root : null;
    }

    void RebuildIndex()
    {
        _index.Clear();
        foreach (var e in modes)
        {
            if (e == null) continue;
            if (_index.ContainsKey(e.mode))
            {
                Debug.LogWarning($"[UIModeManager] Duplicate mode entry: {e.mode}. The later one will overwrite.");
            }
            _index[e.mode] = e;
        }
    }

    IEnumerator Fade(CanvasGroup cg, float from, float to)
    {
        if (!cg) yield break;
        float t = 0f;
        SetAlpha(cg, from);

        while (t < fadeTime)
        {
            t += Time.unscaledDeltaTime;
            float k = fadeTime <= 0f ? 1f : (t / fadeTime);
            SetAlpha(cg, Mathf.Lerp(from, to, k));
            yield return null;
        }

        SetAlpha(cg, to);
    }

    static void SetAlpha(CanvasGroup cg, float a)
    {
        if (!cg) return;
        cg.alpha = a;
    }

    static void SetInteractable(CanvasGroup cg, bool on)
    {
        if (!cg) return;
        cg.interactable = on;
        cg.blocksRaycasts = on;
    }
}
