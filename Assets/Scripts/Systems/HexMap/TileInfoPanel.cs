using TMPro;
using UnityEngine;
using ProjectSulamith.Core;
using ProjectSulamith.Systems;

public class TileInfoPanel : MonoBehaviour
{
    public TextMeshProUGUI terrainText;
    public TextMeshProUGUI positionText;
    public TextMeshProUGUI buildingText; 

    private HexTileData currentTile;

    void OnEnable()
    {
        EventBus.Instance?.Subscribe<BuildingPlacedEvent>(OnBuildingPlaced);
    }

    void OnDisable()
    {
        if (EventBus.Instance == null) return;
        EventBus.Instance.Unsubscribe<BuildingPlacedEvent>(OnBuildingPlaced);
    }

    public void Show(HexTileData tile)
    {
        currentTile = tile;
        gameObject.SetActive(true);
        Refresh();
    }

    public void Hide()
    {
        currentTile = null;
        gameObject.SetActive(false);
    }

    private void Refresh()
    {
        if (currentTile == null) return;

        terrainText.text = $"Terrain: {currentTile.terrainType}";
        positionText.text = $"Pos: {currentTile.cellPosition}";

        
        if (currentTile.hasBuilding)
            buildingText.text = $"Building: {currentTile.buildingPrototypeId}";
        else
            buildingText.text = "Building: (None)";
    }

    private void OnBuildingPlaced(BuildingPlacedEvent e)
    {
        if (currentTile == null) return;
        if (currentTile.cellPosition != e.CellPosition) return;

        
        Refresh();
    }
}//
