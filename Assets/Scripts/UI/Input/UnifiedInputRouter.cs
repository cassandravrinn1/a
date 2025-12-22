using UnityEngine;
using UnityEngine.EventSystems;

public class UnifiedInputRouter : MonoBehaviour
{
    [Header("Receivers")]
    public HexGridInputController hexGridReceiver;

    [Header("Options")]
    [Tooltip("当指针在 UI 上时，路由器是否仍然把输入转发给接收者。一般建议 false，让接收者自己做 UI 阻挡。")]
    public bool forwardWhenPointerOverUI = true;

    void Awake()
    {
        if (hexGridReceiver == null)
            Debug.LogWarning("[UnifiedInputRouter] hexGridReceiver is null.");
    }

    void Update()
    {
        if (hexGridReceiver == null) return;
        if (!hexGridReceiver.isActiveAndEnabled) return;

        // 统一入口：只在这里读 Input
        Vector2 pointerScreenPos = Input.mousePosition;

        bool pointerOverUI =
            EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject();

        // 如果你希望 Router 在 UI 上直接拦截（可选）
        if (!forwardWhenPointerOverUI && pointerOverUI)
        {
            // 注意：这里不转发 hover 也不转发 click
            return;
        }

        // Hover 每帧转发（用于高亮）
        hexGridReceiver.OnPointerMove(pointerScreenPos, pointerOverUI);

        // Click 仅在按下时转发
        if (Input.GetMouseButtonDown(0))
        {
            hexGridReceiver.OnPointerClick(pointerScreenPos, pointerOverUI);
        }
    }
}
