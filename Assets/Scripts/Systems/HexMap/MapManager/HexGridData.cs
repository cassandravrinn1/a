using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class HexGridData : MonoBehaviour
{
    [Header("Tilemap Reference (for bounds/world conversion)")]
    public Tilemap groundTilemap;

    // 地图逻辑数据
    private readonly Dictionary<Vector3Int, HexTileData> _hexTiles = new Dictionary<Vector3Int, HexTileData>();

    // 当前选中
    public Vector3Int SelectedCell { get; private set; }
    public HexTileData SelectedTileData { get; private set; }

    // ===============================
    // 邻接逻辑：按你当前实现：用 cellPos.y 奇偶
    //（注：如果你未来改回 Odd-Q，请把这里改成 cellPos.x）
    // ===============================
    private static readonly Vector3Int[] EvenColumnDirections =
    {
        new Vector3Int(+1, 0, 0), // 上
        new Vector3Int( 0, +1, 0), // 右上
        new Vector3Int(-1, 0, 0), // 下
        new Vector3Int( 0,-1, 0), // 左上
        new Vector3Int(-1,+1, 0), // 右下
        new Vector3Int(-1,-1, 0), // 左下
    };

    private static readonly Vector3Int[] OddColumnDirections =
    {
        new Vector3Int(+1, 0, 0), // 上
        new Vector3Int(+1,+1, 0), // 右上
        new Vector3Int(-1, 0, 0), // 下
        new Vector3Int(+1,-1, 0), // 左上
        new Vector3Int( 0,+1, 0), // 右下
        new Vector3Int( 0,-1, 0), // 左下
    };

    void Awake()
    {
        if (groundTilemap == null)
            Debug.LogError("[HexGridData] groundTilemap is null.");
    }

    void Start()
    {
        InitializeGridData();
    }

    // ===============================
    // 初始化
    // ===============================
    public void InitializeGridData()
    {
        _hexTiles.Clear();
        if (groundTilemap == null) return;

        BoundsInt bounds = groundTilemap.cellBounds;
        foreach (Vector3Int pos in bounds.allPositionsWithin)
        {
            if (!groundTilemap.HasTile(pos))
                continue;

            HexTileData data = new HexTileData
            {
                cellPosition = pos,
                terrainType = TerrainType.Grassland,
                hasBuilding = false
            };

            _hexTiles[pos] = data;
        }

        Debug.Log($"[HexGridData] Initialized: {_hexTiles.Count} tiles");
    }

    // ===============================
    // 查询（保留原接口）
    // ===============================
    public bool HasTile(Vector3Int cellPos) => _hexTiles.ContainsKey(cellPos);

    // 原接口：GetTileData(Vector3Int)
    public HexTileData GetTileData(Vector3Int cellPos)
    {
        return _hexTiles.TryGetValue(cellPos, out HexTileData data) ? data : null;
    }

    // 原接口：GetCellCenterWorld(Vector3Int)

    public Vector3 GetCellCenterWorld(Vector3Int cellPos)
    {
        return groundTilemap != null ? groundTilemap.GetCellCenterWorld(cellPos) : Vector3.zero;
    }

    // 原接口：GetNeighbors(Vector3Int)
    public List<Vector3Int> GetNeighbors(Vector3Int cellPos)
    {
        List<Vector3Int> neighbors = new List<Vector3Int>();

        bool isOdd = (cellPos.y & 1) == 1; // 保留你当前做法：按 y 奇偶
        Vector3Int[] directions = isOdd ? OddColumnDirections : EvenColumnDirections;

        foreach (Vector3Int dir in directions)
        {
            Vector3Int neighbor = cellPos + dir;
            if (_hexTiles.ContainsKey(neighbor))
                neighbors.Add(neighbor);
        }

        return neighbors;
    }
    /// <summary>
    /// 执行全局协程（避免弹窗销毁导致协程终止）
    /// </summary>
    public Coroutine StartGlobalCoroutine(IEnumerator routine)
    {
        return StartCoroutine(routine);
    }
    // ===============================
    // 选择（保留语义）
    // ===============================
    public void SelectCell(Vector3Int cellPos)
    {
        if (!_hexTiles.ContainsKey(cellPos)) return;

        SelectedCell = cellPos;
        SelectedTileData = _hexTiles[cellPos];

        OnTileClicked(SelectedTileData);
    }

    // 原 HexGridManager 对外接口：GetSelectedTile()
    public HexTileData GetSelectedTile()
    {
        return SelectedTileData;
    }
    private void OnTileClicked(HexTileData tile)
    {
        if (tile == null) return;
        Debug.Log($"[HexGridData] 点击地块：{tile.cellPosition}，是否有建筑：{tile.hasBuilding}");
        if (tile.isBuilding)
        {
            Debug.Log($"地块 {tile.cellPosition} 正在建造中，禁止点击交互");
            // 可选：显示提示文本（比如UI上飘字“建筑正在建造中...”）
            return;
        }
        // 检查PopupRootManager是否存在
        if (PopupRootManager.Instance == null)
        {
            Debug.LogError("[HexGridData] PopupRootManager 实例不存在！");
            return;
        }
        if (!tile.isBuilding)
        {
            // 核心逻辑：无建筑→弹出建造弹窗；有建筑→弹出派遣弹窗
            if (!tile.hasBuilding)
            {
                // 弹出建造选择弹窗
                PopupRootManager.Instance.ShowBuildSelectPopup(tile);
            }
            else
            {
                // 弹出派遣弹窗（传入建筑实例ID）
                if (!string.IsNullOrEmpty(tile.buildingInstanceId))
                {
                    PopupRootManager.Instance.ShowBuildingAssignUI(tile.buildingInstanceId);
                }
                else
                {
                    Debug.LogWarning($"[HexGridData] 地块有建筑但实例ID为空：{tile.cellPosition}");
                }
            }
        }
    }

    // 兼容原有SetSelectedTile方法（如果外部有调用）
    public void SetSelectedTile(HexTileData tile)
    {
        if (tile == null) return;
        SelectedCell = tile.cellPosition;
        SelectedTileData = tile;
    }
    /// <summary>
    /// 获取所有地块数据（新增方法）
    /// </summary>
    public List<HexTileData> GetAllTiles()
    {
        return new List<HexTileData>(_hexTiles.Values);
    }
}

// ===============================
// 数据结构
// ===============================
public enum TerrainType
{
    Grassland,
    Plains,
    Desert,
    Mountain,
    Water
}

public class HexTileData
{
    public Vector3Int cellPosition;
    public TerrainType terrainType;
    public bool hasBuilding;
    public bool isBuilding; // 是否正在建造
    public float buildRemainingTime; // 剩余建造时长

    public string buildingPrototypeId;
    public string buildingInstanceId;
    public GameObject buildingInstance;

}
