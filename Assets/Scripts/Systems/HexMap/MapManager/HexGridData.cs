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

    // ===============================
    // 选择（保留语义）
    // ===============================
    public void SelectCell(Vector3Int cellPos)
    {
        if (!_hexTiles.ContainsKey(cellPos)) return;

        SelectedCell = cellPos;
        SelectedTileData = _hexTiles[cellPos];
    }

    // 原 HexGridManager 对外接口：GetSelectedTile()
    public HexTileData GetSelectedTile()
    {
        return SelectedTileData;
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
    public string buildingPrototypeId;
    public GameObject buildingInstance;
}
