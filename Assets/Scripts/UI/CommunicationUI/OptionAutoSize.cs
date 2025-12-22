using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class OptionAutoSize : MonoBehaviour
{
    [Header("Refs")]
    public TextMeshProUGUI label;      // 按钮里的 TMP 文本
    public LayoutElement layout;       // 挂在按钮上的 LayoutElement

    [Header("Padding")]
    public float paddingHorizontal = 32f;  // 左右内边距总和（例如左右各 16）
    public float paddingVertical = 16f;  // 上下内边距总和（例如上下各 8）

    [Header("Width Limit")]
    public float minWidth = 160f;      // 最小宽度
    public float maxWidth = 400f;      // 最大宽度（一行太长就换行）

    [Header("Height Limit")]
    public float minHeight = 40f;      // 一行按钮的大致高度
    public float maxHeight = 200f;     // 防止变成超大方块

    /// <summary>
    /// 在 ChatUIController 里设置完文本后调用。
    /// </summary>
    public void SetText(string text)
    {
        if (label == null || layout == null)
            return;

        // 1. 填文字 + 开启换行
        label.enableWordWrapping = true;
        label.text = text;

        // 2. 让 TMP 在「最大宽度 - padding」这个约束下，计算理想宽高
        //    第二个参数是可用宽度（不包括左右内边距）
        float availableWidth = Mathf.Max(0f, maxWidth - paddingHorizontal);
        Vector2 preferred = label.GetPreferredValues(text, availableWidth, 0);

        // 3. 文本宽度：在 [minWidth - padding, maxWidth - padding] 之间
        float textWidth = Mathf.Clamp(
            preferred.x,
            Mathf.Max(0f, minWidth - paddingHorizontal),
            Mathf.Max(0f, maxWidth - paddingHorizontal)
        );

        float targetWidth = textWidth + paddingHorizontal;
        float targetHeight = preferred.y + paddingVertical;

        // 4. 高度限制一下，避免离谱
        targetHeight = Mathf.Clamp(targetHeight, minHeight, maxHeight);

        // 5. 只改 LayoutElement，让 VerticalLayoutGroup 决定最终尺寸
        layout.preferredWidth = targetWidth;
        layout.preferredHeight = targetHeight;

        // 6. 不再改 label.rectTransform，让它保持 stretch，由父物体+padding 决定显示区域
    }
}
