// BuildingDef.cs
using UnityEngine;

[CreateAssetMenu(menuName = "Sulamith/BuildingDef")]
public class BuildingDef : ScriptableObject
{
    public string id;
    [Header("Integer costs only")]
    public int costFood;
    public int costMat;
    public int costEnergy;
    [Header("Build time in GAME seconds")]
    public float buildTimeGameSeconds = 30f;
    [Header("On complete (optional)")]
    public int addFoodCap, addMatCap, addEnergyCap;
}
