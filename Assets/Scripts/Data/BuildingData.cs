using UnityEngine;

namespace ManInTheYear.Data
{
    /// <summary>
    /// ScriptableObject that describes a placeable building (e.g. Sawmill, Mine, Watchtower).
    /// Separating build definitions into assets lets designers create new buildings without
    /// touching any C# code (Open/Closed Principle).
    /// </summary>
    [CreateAssetMenu(fileName = "NewBuilding", menuName = "Man In The Year/Building Data")]
    public class BuildingData : ScriptableObject
    {
        [Header("Identity")]
        public string buildingId;
        public string displayName;
        public Sprite previewIcon;

        [Header("Prefab")]
        [Tooltip("Prefab instantiated when the building is placed. Should use GPU instancing.")]
        public GameObject buildingPrefab;

        [Tooltip("Semi-transparent ghost prefab shown during placement. Share materials with the real prefab.")]
        public GameObject ghostPrefab;

        [Header("Cost")]
        [Tooltip("Resources required to place this building.")]
        public ResourceCost[] buildCost;

        [Header("Footprint")]
        [Tooltip("Grid cells occupied on the tile map (width x height).")]
        public Vector2Int footprintSize = Vector2Int.one;

        [Header("Production")]
        [Tooltip("If this building auto-produces resources, define them here.")]
        public ResourceProduction[] productionOutputs;

        [Tooltip("Seconds between production ticks.")]
        [Min(1f)]
        public float productionIntervalSeconds = 10f;
    }

    /// <summary>Single entry in a build-cost list.</summary>
    [System.Serializable]
    public struct ResourceCost
    {
        public ResourceData resource;
        [Min(1)] public int amount;
    }

    /// <summary>Single entry in a production output list.</summary>
    [System.Serializable]
    public struct ResourceProduction
    {
        public ResourceData resource;
        [Min(1)] public int amountPerTick;
    }
}
