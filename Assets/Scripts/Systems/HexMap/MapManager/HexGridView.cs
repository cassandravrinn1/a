using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class HexGridView : MonoBehaviour
{
    [Header("Tilemaps")]
    public Tilemap highlightTilemap;

    [Header("Highlight Tile")]
    public TileBase highlightTile;

    void Awake()
    {
        if (highlightTilemap == null)
            Debug.LogError("[HexGridView] highlightTilemap is null.");
        if (highlightTile == null)
            Debug.LogError("[HexGridView] highlightTile is null.");
    }

    public void ClearHighlight()
    {
        if (highlightTilemap == null) return;
        highlightTilemap.ClearAllTiles();
    }

    public void HighlightCells(Vector3Int center, List<Vector3Int> neighbors)
    {
        if (highlightTilemap == null || highlightTile == null) return;

        highlightTilemap.SetTile(center, highlightTile);

        if (neighbors != null)
        {
            foreach (var n in neighbors)
                highlightTilemap.SetTile(n, highlightTile);
        }
    }
}
