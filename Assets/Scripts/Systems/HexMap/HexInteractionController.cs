/*using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class HexGridManager : MonoBehaviour
{
    [Header("Tilemaps")]
    public Tilemap groundTilemap;
    public Tilemap highlightTilemap;
    //这里放图层
    [Header("Highlight Tile")]
    public TileBase highlightTile;

    //目前选中地块
    private Vector3Int selectedCell;
    private HexTileData selectedTileData;
    //引用信息面板
    public TileInfoPanel tileInfoPanel;

    // ===============================
    //邻接逻辑：分奇偶行讨论
    // ===============================

    private static readonly Vector3Int[] EvenColumnDirections =
    {
        new Vector3Int(+1, 0, 0),//上
        new Vector3Int( 0, +1, 0),//右上
        new Vector3Int(-1,  0, 0),//下
        new Vector3Int(0, -1, 0),//左上
        new Vector3Int( -1, +1, 0),//右下
        new Vector3Int(-1, -1, 0),//左下
    };

    private static readonly Vector3Int[] OddColumnDirections =
    {
        new Vector3Int(+1, 0, 0),//上
        new Vector3Int( +1, +1, 0),//右上
        new Vector3Int(-1,  0, 0),//下
        new Vector3Int(+1, -1, 0),//左上
        new Vector3Int( 0, +1, 0),//右下
        new Vector3Int(0, -1, 0),//左下
    };

    // 地图逻辑数据
    private Dictionary<Vector3Int, HexTileData> hexTiles =
        new Dictionary<Vector3Int, HexTileData>();

    private Vector3Int currentHoverCell;
    private bool hasHoverCell = false;

    void Start()
    {
        InitializeGridData();
    }

    void Update()
    {
        HandleMouseHover();//鼠标移动触发
        HandleMouseClick();//鼠标点击触发

    }

    // ===============================
    // 初始化
    // ===============================

    void InitializeGridData()
    {
        hexTiles.Clear();

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

            hexTiles[pos] = data;
        }

        Debug.Log($"Hex Grid Initialized: {hexTiles.Count} tiles");
    }

    // ===============================
    // 鼠标交互
    // ===============================
    //鼠标移动
    void HandleMouseHover()
    {
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        worldPos.z = 0f;

        Vector3Int cellPos = groundTilemap.WorldToCell(worldPos);

        if (!hexTiles.ContainsKey(cellPos))
        {
            ClearHighlight();
            return;
        }

        if (!hasHoverCell || cellPos != currentHoverCell)
        {
            ClearHighlight();
            HighlightCell(cellPos);
            currentHoverCell = cellPos;
            hasHoverCell = true;
        }
    }
    //鼠标点击
    void HandleMouseClick()
    {
        if (!Input.GetMouseButtonDown(0))
            return;

        Vector3 worldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        worldPos.z = 0f;

        Vector3Int cellPos = groundTilemap.WorldToCell(worldPos);

        if (!hexTiles.ContainsKey(cellPos))
            return;

        OnTileClicked(cellPos);
    }
    void OnTileClicked(Vector3Int cellPos)
    {
        selectedCell = cellPos;
        selectedTileData = hexTiles[cellPos];

        tileInfoPanel.Show(selectedTileData);
    }

    //选中地块

    public HexTileData GetSelectedTile()
    {
        return selectedTileData;
    }
    //ui用


    //高亮（切换显示）
    void HighlightCell(Vector3Int cellPos)
    {
        highlightTilemap.SetTile(cellPos, highlightTile);

        foreach (Vector3Int neighbor in GetNeighbors(cellPos))
        {
            highlightTilemap.SetTile(neighbor, highlightTile);
        }
    }
    //清除高亮状态
    void ClearHighlight()
    {
        highlightTilemap.ClearAllTiles();
        hasHoverCell = false;
    }

    // ===============================
    // Hex 核心逻辑（Flat Top + Odd-Q）
    // ===============================

    public List<Vector3Int> GetNeighbors(Vector3Int cellPos)
    {
        List<Vector3Int> neighbors = new List<Vector3Int>();

        // Flat Top 使用行偏移（Odd-Q）
        bool isOddColumn = (cellPos.y & 1) == 1;//*.x->.y
        Vector3Int[] directions =
            isOddColumn ? OddColumnDirections : EvenColumnDirections;

        foreach (Vector3Int dir in directions)
        {
            Vector3Int neighbor = cellPos + dir;
            if (hexTiles.ContainsKey(neighbor))
            {
                neighbors.Add(neighbor);
            }
        }

        return neighbors;
    }

    public Vector3 GetCellCenterWorld(Vector3Int cellPos)
    {
        return groundTilemap.GetCellCenterWorld(cellPos);
    }

    public bool HasTile(Vector3Int cellPos)
    {
        return hexTiles.ContainsKey(cellPos);
    }

    public HexTileData GetTileData(Vector3Int cellPos)
    {
        return hexTiles.TryGetValue(cellPos, out HexTileData data) ? data : null;
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
    public Vector3Int cellPosition;//位置
    public TerrainType terrainType;
    public bool hasBuilding;
    public string buildingPrototypeId;//建筑名称
    public GameObject buildingInstance;
}
*/