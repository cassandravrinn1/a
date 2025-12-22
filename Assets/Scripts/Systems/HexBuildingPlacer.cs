using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using ProjectSulamith.Core;
using ProjectSulamith.Systems;

public class HexBuildingPlacer : MonoBehaviour
{
    public HexGridData hexGrid;
    public Tilemap groundTilemap;

    [Header("Building Prefabs")]
    public GameObject warehousePrefab;
    public GameObject batteryPrefab;
    public GameObject canteenPrefab;

    [Header("Hierarchy / Parent")]
    [Tooltip("建筑实例的父物体。建议拖 MapRoot/Buildings 之类的容器，确保跟地图一起显隐。")]
    public Transform buildingParent;

    private Dictionary<System.Guid, Vector3Int> pendingBuilds = new Dictionary<System.Guid, Vector3Int>();

    private EventBus _bus;

    void OnEnable()
    {
        _bus = EventBus.Instance;
        _bus?.Subscribe<BuildAccepted>(OnBuildAccepted);

        // 重要：你原来写了 OnBuildRequest 但没订阅，这里顺手补上（不影响本问题，但能让 pendingBuilds 真正生效）
        _bus?.Subscribe<BuildRequest>(OnBuildRequest);
    }

    void OnDisable()
    {
        if (_bus != null)
        {
            _bus.Unsubscribe<BuildAccepted>(OnBuildAccepted);
            _bus.Unsubscribe<BuildRequest>(OnBuildRequest);
        }
        _bus = null;
    }

    void Awake()
    {
        // 如果你没在 Inspector 里拖 buildingParent，就默认用自己（推荐你在场景里建一个 Buildings 容器再拖进来）
        if (buildingParent == null)
            buildingParent = this.transform;
    }

    void OnBuildRequest(BuildRequest req)
    {
        pendingBuilds[req.TxId] = req.CellPosition;
        Debug.Log($"[Placer] OnBuildRequest TxId={req.TxId} cell={req.CellPosition} proto={req.PrototypeId}");
    }

    void OnBuildAccepted(BuildAccepted e)
    {
        if (hexGrid == null) { Debug.LogError("[Placer] hexGrid NULL"); return; }

        var cell = e.CellPosition;

        var tile = hexGrid.GetTileData(cell);
        if (tile == null || tile.hasBuilding) return;

        var prefab = GetPrefab(e.PrototypeId);
        if (prefab == null) { Debug.LogError($"[Placer] prefab NULL for {e.PrototypeId}"); return; }

        var worldPos = hexGrid.GetCellCenterWorld(cell);

        // 关键改动：指定 parent
        var go = Instantiate(prefab, worldPos, Quaternion.identity, buildingParent);

        tile.hasBuilding = true;
        tile.buildingPrototypeId = e.PrototypeId;
        tile.buildingInstance = go;

        _bus?.Publish(new BuildingPlacedEvent { CellPosition = cell, PrototypeId = e.PrototypeId });
    }

    GameObject GetPrefab(string proto)
    {
        switch (proto)
        {
            case "Warehouse": return warehousePrefab;
            case "Battery": return batteryPrefab;
            case "Canteen": return canteenPrefab;
            default: return null;
        }
    }
}
