using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

namespace ProjectSulamith.TechTree
{
    public class TechNodeView : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerClickHandler
    {
        [Header("引用")]
        public Image baseCircle;
        public Image icon;
        public Image glow;
        public TMP_Text label;

        [Header("状态颜色")]
        public Color lockedColor = new Color(0.2f, 0.4f, 0.7f);   // 暗蓝
        public Color availableColor = new Color(0.8f, 0.8f, 0.2f); // 黄
        public Color unlockedColor = new Color(0.3f, 0.9f, 0.5f);  // 绿

        // 当前绑定的数据
        public TechNodeData Data { get; private set; }
        public bool IsUnlocked { get; private set; }
        public bool IsAvailable { get; private set; }

        private TechTreeUI _treeUI;
        private RectTransform _rect;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
        }

        public void Initialize(TechNodeData data, TechTreeUI treeUI, bool unlocked, bool available)
        {
            Data = data;
            _treeUI = treeUI;
            IsUnlocked = unlocked;
            IsAvailable = available;

            if (label != null)
                label.text = data.displayName;

            // 把 ScriptableObject 里的坐标应用到 UI 上
            _rect.anchoredPosition = data.uiPosition;

            UpdateVisual();
        }

        public Vector3 GetWorldPosition()
        {
            return _rect.position; // 用于 LineRenderer 取端点
        }

        private void UpdateVisual()
        {
            // 简单状态控制：用 Glow 的颜色 + Base 的颜色
            if (IsUnlocked)
            {
                baseCircle.color = unlockedColor;
                if (glow != null) glow.color = unlockedColor * 0.7f;
            }
            else if (IsAvailable)
            {
                baseCircle.color = availableColor;
                if (glow != null) glow.color = availableColor * 0.5f;
            }
            else
            {
                baseCircle.color = lockedColor;
                if (glow != null) glow.color = lockedColor * 0.3f;
            }

            // 初始 Glow 尺寸
            if (glow != null)
            {
                glow.rectTransform.localScale = Vector3.one;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            // Hover：简单做个放大 + 提高透明度，之后可以换成 Shader Ripple
            if (glow != null)
            {
                glow.rectTransform.localScale = Vector3.one * 1.2f;
                var c = glow.color;
                c.a = Mathf.Clamp01(c.a + 0.2f);
                glow.color = c;
            }

            _treeUI?.OnNodeHovered(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (glow != null)
            {
                glow.rectTransform.localScale = Vector3.one;
                var c = glow.color;
                c.a = Mathf.Clamp01(c.a - 0.2f);
                glow.color = c;
            }

            _treeUI?.OnNodeHoverExit(this);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            _treeUI?.OnNodeClicked(this);
        }

        // 外部在解锁状态变化时调用
        public void SetState(bool unlocked, bool available)
        {
            IsUnlocked = unlocked;
            IsAvailable = available;
            UpdateVisual();
        }
    }
}
