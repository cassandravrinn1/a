using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 单条聊天气泡的自动排版脚本。
/// - 挂在 Bubble（气泡本体）上；
/// - 自动向上寻找父物体中的 LayoutElement，当成 Row 的 LayoutElement 使用。
/// </summary>
public class ChatBubbleAutoSize : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("气泡里的 TMP 文本组件（在 Bubble 下方）")]
    public TextMeshProUGUI messageText;       // 文本

    [Tooltip("挂在『行 Row』上的 LayoutElement（如果留空，会自动在父物体中寻找）")]
    public LayoutElement rowLayoutElement;    // 行的 LayoutElement

    [Tooltip("气泡本体的 RectTransform（通常就是本脚本所在的 RectTransform）")]
    public RectTransform bubbleRect;          // 气泡自身的 RectTransform

    [Header("Width Settings")]
    [Tooltip("最小气泡宽度（包含左右 padding）")]
    public float minWidth = 80f;

    [Tooltip("最大气泡宽度（包含左右 padding）")]
    public float maxWidth = 500f;

    [Tooltip("左右总 padding（例如左右各 16 就填 32）")]
    public float horizontalPadding = 32f;     // 左右总 padding

    [Tooltip("上下总 padding（例如上下各 8 就填 16）")]
    public float verticalPadding = 16f;       // 上下总 padding

    private void Awake()
    {
        if (!bubbleRect)
            bubbleRect = transform as RectTransform;

        // ★ 自动在父物体中寻找 Row 的 LayoutElement，省得手动拖
        if (!rowLayoutElement)
            rowLayoutElement = GetComponentInParent<LayoutElement>();
    }

    /// <summary>
    /// 对外接口：设置文本并自动刷新尺寸。
    /// </summary>
    public void SetText(string content)
    {
        if (!messageText) return;

        messageText.text = content;
        Refresh();
    }

    /// <summary>
    /// 根据当前文本内容重新计算气泡和所在行的宽高。
    /// </summary>
    public void Refresh()
    {
        if (!messageText || !rowLayoutElement || !bubbleRect)
            return;

        messageText.enableWordWrapping = true;
        messageText.ForceMeshUpdate();

        // 1）完全不受限制时的一行宽度
        Vector2 unconstrained = messageText.GetPreferredValues(
            messageText.text,
            Mathf.Infinity,
            Mathf.Infinity
        );

        // 文本可用宽度（不含 padding）
        float minTextWidth = Mathf.Max(1f, minWidth - horizontalPadding);
        float maxTextWidth = Mathf.Max(minTextWidth, maxWidth - horizontalPadding);
        float textWidth = Mathf.Clamp(unconstrained.x, minTextWidth, maxTextWidth);

        // 气泡整体宽度（含 padding）
        float bubbleWidth = textWidth + horizontalPadding;

        // 2）在该宽度下计算高度（考虑换行）
        Vector2 atWidth = messageText.GetPreferredValues(
            messageText.text,
            textWidth,
            Mathf.Infinity
        );

        float bubbleHeight = atWidth.y + verticalPadding;

        // 3）修改气泡自身的 RectTransform（视觉大小）
        bubbleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bubbleWidth);
        bubbleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bubbleHeight);

        // 4）把高度写到「行 Row」的 LayoutElement，供 Content 的 VerticalLayoutGroup 使用
        rowLayoutElement.preferredHeight = bubbleHeight;
        // 宽度让 Row 靠锚点 + Padding 控制，这里不用强设
        rowLayoutElement.preferredWidth = -1f;

        // 5）重建布局
        RectTransform rowRT = rowLayoutElement.transform as RectTransform;
        if (rowRT != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rowRT);

            var contentRT = rowRT.parent as RectTransform;
            if (contentRT != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRT);
        }
    }

    private void OnEnable()
    {
        Refresh();
    }
}
