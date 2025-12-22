using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Tilemaps;

public class HexGridInputController : MonoBehaviour
{
    [Header("Refs")]
    public Camera worldCamera;
    public HexGridData data;
    public HexGridView view;

    [Header("UI")]
    public TileInfoPanel tileInfoPanel;

    [Header("Options")]
    public bool blockWhenPointerOverUI = true;

    Vector3Int _hoverCell;
    bool _hasHover;

    void Awake()
    {
        if (worldCamera == null) worldCamera = Camera.main;
        if (worldCamera == null) Debug.LogError("[HexGridInputController] worldCamera is null (no Camera.main).");
        if (data == null) Debug.LogError("[HexGridInputController] data is null.");
        if (view == null) Debug.LogError("[HexGridInputController] view is null.");
        if (tileInfoPanel == null) Debug.LogWarning("[HexGridInputController] tileInfoPanel is null (click will not show info).");
    }

    // 注意：Update 已移除。输入由 UnifiedInputRouter 统一读取并调用下面两个方法。

    /// <summary>
    /// 由统一输入路由器调用：指针移动（用于 hover 高亮）。
    /// pointerOverUI 由路由器传入；你也可以不传，直接让接收者自行判断 UI。
    /// </summary>
    public void OnPointerMove(Vector2 screenPos, bool pointerOverUI)
    {
        if (!enabled) return;
        if (ShouldBlock(pointerOverUI)) return;

        HandleHover(screenPos);
    }

    /// <summary>
    /// 由统一输入路由器调用：指针点击（用于选中格子与显示信息）。
    /// </summary>
    public void OnPointerClick(Vector2 screenPos, bool pointerOverUI)
    {
        if (!enabled) return;
        if (ShouldBlock(pointerOverUI)) return;

        HandleClick(screenPos);
    }

    bool ShouldBlock(bool pointerOverUIFromRouter)
    {
        if (!blockWhenPointerOverUI) return false;

        // 优先使用路由器传入的结果（更稳定）
        if (pointerOverUIFromRouter) return true;

        // 保险：如果路由器没算/没传（例如你手动调用 OnPointerClick），这里再检查一次
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return true;

        return false;
    }

    void HandleHover(Vector2 screenPos)
    {
        if (worldCamera == null || data == null || data.groundTilemap == null || view == null) return;

        Vector3 worldPos = worldCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f));
        worldPos.z = 0f;

        Vector3Int cellPos = data.groundTilemap.WorldToCell(worldPos);

        if (!data.HasTile(cellPos))
        {
            if (_hasHover)
            {
                view.ClearHighlight();
                _hasHover = false;
            }
            return;
        }

        if (!_hasHover || cellPos != _hoverCell)
        {
            view.ClearHighlight();
            view.HighlightCells(cellPos, data.GetNeighbors(cellPos));
            _hoverCell = cellPos;
            _hasHover = true;
        }
    }

    void HandleClick(Vector2 screenPos)
    {
        if (worldCamera == null || data == null || data.groundTilemap == null) return;

        Vector3 worldPos = worldCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f));
        worldPos.z = 0f;

        Vector3Int cellPos = data.groundTilemap.WorldToCell(worldPos);
        if (!data.HasTile(cellPos)) return;

        data.SelectCell(cellPos);

        if (tileInfoPanel != null)
            tileInfoPanel.Show(data.SelectedTileData);
    }

    // ===== 保留原来的接口（外部例如 UIModeManager 调用） =====
    public void SetInputEnabled(bool enabled)
    {
        // 保留接口名与语义：控制该 Receiver 是否工作
        this.enabled = enabled;

        if (!this.enabled && view != null)
        {
            view.ClearHighlight();
            _hasHover = false;
        }
    }
}
