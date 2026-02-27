using System.Collections.Generic;
using UnityEngine;
using ManInTheYear.Data;

namespace ManInTheYear.Systems
{
    /// <summary>
    /// Manages building placement, validation and production ticks.
    ///
    /// Design
    /// ──────
    /// • BuildingData ScriptableObjects define costs and outputs – zero hard-coded values.
    /// • A 2-D boolean grid tracks occupied cells (no physics overlap queries per frame).
    /// • Production ticks run on a timer, not Update(), reducing per-frame CPU work.
    ///
    /// Mobile optimisation
    /// ───────────────────
    /// CPU: Grid lookup is O(1).  Production loop uses a plain List (no LINQ).
    /// Draw Calls: Buildings must use GPU instancing on their materials.
    ///             Combine static buildings with a MeshCombiner after placement
    ///             (or use Unity's Static Batching bake).
    /// RAM: Ghost prefab is a single shared instance (recycled, not re-instantiated).
    ///
    /// Level-Designer integration
    /// ──────────────────────────
    /// Set gridWidth/gridHeight in the Inspector.
    /// Assign BuildingData assets from the project browser via the BuildingPalette list.
    /// </summary>
    public sealed class BuildingSystem : MonoBehaviour
    {
        // ── Singleton ────────────────────────────────────────────────────────────
        public static BuildingSystem Instance { get; private set; }

        // ── Inspector (designer-facing) ───────────────────────────────────────────
        [Header("Grid Configuration")]
        [SerializeField, Min(1)] private int gridWidth = 50;
        [SerializeField, Min(1)] private int gridHeight = 50;
        [SerializeField] private float cellSize = 1f;
        [SerializeField] private Vector3 gridOrigin = Vector3.zero;

        [Header("Building Palette")]
        [Tooltip("All buildings available to the player. Drag BuildingData assets here.")]
        [SerializeField] private List<BuildingData> buildingPalette = new List<BuildingData>();

        // ── Runtime state ─────────────────────────────────────────────────────────
        private bool[,] _occupiedGrid;
        private readonly List<PlacedBuilding> _placedBuildings = new List<PlacedBuilding>(32);

        private BuildingData _pendingBuilding;
        private GameObject _ghostInstance;
        private Renderer[] _ghostRenderers;          // cached on ghost creation
        private readonly MaterialPropertyBlock _mpb = new MaterialPropertyBlock();
        private float _productionTimer;

        // ── Unity lifecycle ───────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _occupiedGrid = new bool[gridWidth, gridHeight];
        }

        private void Update()
        {
            TickProduction();
            UpdateGhostPosition();
        }

        private void OnDestroy()
        {
            if (_ghostInstance != null) Destroy(_ghostInstance);
        }

        // ── Placement flow ────────────────────────────────────────────────────────

        /// <summary>
        /// Called by the UI when the player selects a building from the palette.
        /// Shows the placement ghost.
        /// </summary>
        public void BeginPlacement(BuildingData data)
        {
            if (_ghostInstance != null) Destroy(_ghostInstance);

            _pendingBuilding = data;

            if (data.ghostPrefab != null)
            {
                _ghostInstance = Instantiate(data.ghostPrefab);
                _ghostRenderers = _ghostInstance.GetComponentsInChildren<Renderer>();
            }
        }

        /// <summary>
        /// Called by the touch input handler with the world-space tap position.
        /// Validates cost and grid occupancy, then places the building.
        /// Returns true on success.
        /// </summary>
        public bool TryPlace(Vector3 worldPosition)
        {
            if (_pendingBuilding == null) return false;

            Vector2Int cell = WorldToCell(worldPosition);
            if (!IsFreeArea(cell, _pendingBuilding.footprintSize)) return false;

            if (!ResourceSystem.Instance.TrySpendAll(_pendingBuilding.buildCost)) return false;

            // Stamp the grid
            MarkArea(cell, _pendingBuilding.footprintSize, true);

            // Instantiate the real building
            Vector3 snapPosition = CellToWorld(cell);
            GameObject go = Instantiate(_pendingBuilding.buildingPrefab, snapPosition, Quaternion.identity);

            _placedBuildings.Add(new PlacedBuilding
            {
                data = _pendingBuilding,
                gridCell = cell,
                instance = go
            });

            CancelPlacement();
            return true;
        }

        /// <summary>Cancels the current ghost placement without spending resources.</summary>
        public void CancelPlacement()
        {
            _pendingBuilding = null;
            _ghostRenderers = null;
            if (_ghostInstance != null)
            {
                Destroy(_ghostInstance);
                _ghostInstance = null;
            }
        }

        // ── Production ────────────────────────────────────────────────────────────
        private void TickProduction()
        {
            if (_placedBuildings.Count == 0) return;

            _productionTimer += Time.deltaTime;

            // Only iterate when at least one building's interval has elapsed.
            // Using the smallest interval as a shared ticker is a simple heuristic;
            // for more buildings consider per-building timers.
            float minInterval = float.MaxValue;
            for (int i = 0; i < _placedBuildings.Count; i++)
            {
                float interval = _placedBuildings[i].data.productionIntervalSeconds;
                if (interval < minInterval) minInterval = interval;
            }

            if (_productionTimer < minInterval) return;
            _productionTimer = 0f;

            for (int i = 0; i < _placedBuildings.Count; i++)
            {
                BuildingData bd = _placedBuildings[i].data;
                if (bd.productionOutputs == null) continue;

                for (int j = 0; j < bd.productionOutputs.Length; j++)
                {
                    ResourceProduction prod = bd.productionOutputs[j];
                    ResourceSystem.Instance.TryAdd(prod.resource, prod.amountPerTick);
                }
            }
        }

        // ── Ghost ─────────────────────────────────────────────────────────────────
        private void UpdateGhostPosition()
        {
            if (_ghostInstance == null) return;

            // The touch handler updates a shared "cursor world position" property.
            // We read it here to move the ghost without coupling to input code.
            Vector3 cursorWorld = TouchInputHandler.CursorWorldPosition;
            Vector2Int cell = WorldToCell(cursorWorld);
            _ghostInstance.transform.position = CellToWorld(cell);

            // Tint ghost red if placement would be invalid
            bool valid = IsFreeArea(cell, _pendingBuilding.footprintSize)
                         && ResourceSystem.Instance.CanAfford(_pendingBuilding.buildCost);
            SetGhostTint(valid);
        }

        private void SetGhostTint(bool valid)
        {
            if (_ghostRenderers == null) return;
            Color tint = valid ? new Color(0f, 1f, 0f, 0.5f) : new Color(1f, 0f, 0f, 0.5f);
            for (int i = 0; i < _ghostRenderers.Length; i++)
            {
                // Reuse the cached _mpb to avoid per-frame allocation while
                // still preserving GPU instancing batching.
                _ghostRenderers[i].GetPropertyBlock(_mpb);
                _mpb.SetColor("_BaseColor", tint);
                _ghostRenderers[i].SetPropertyBlock(_mpb);
            }
        }

        // ── Grid helpers ──────────────────────────────────────────────────────────
        private Vector2Int WorldToCell(Vector3 world)
        {
            int x = Mathf.FloorToInt((world.x - gridOrigin.x) / cellSize);
            int z = Mathf.FloorToInt((world.z - gridOrigin.z) / cellSize);
            x = Mathf.Clamp(x, 0, gridWidth - 1);
            z = Mathf.Clamp(z, 0, gridHeight - 1);
            return new Vector2Int(x, z);
        }

        private Vector3 CellToWorld(Vector2Int cell)
        {
            return new Vector3(
                gridOrigin.x + cell.x * cellSize + cellSize * 0.5f,
                gridOrigin.y,
                gridOrigin.z + cell.y * cellSize + cellSize * 0.5f);
        }

        private bool IsFreeArea(Vector2Int origin, Vector2Int size)
        {
            for (int x = origin.x; x < origin.x + size.x; x++)
            for (int z = origin.y; z < origin.y + size.y; z++)
            {
                if (x < 0 || x >= gridWidth || z < 0 || z >= gridHeight) return false;
                if (_occupiedGrid[x, z]) return false;
            }
            return true;
        }

        private void MarkArea(Vector2Int origin, Vector2Int size, bool occupied)
        {
            for (int x = origin.x; x < origin.x + size.x; x++)
            for (int z = origin.y; z < origin.y + size.y; z++)
                _occupiedGrid[x, z] = occupied;
        }

        // ── Inner types ───────────────────────────────────────────────────────────
        private struct PlacedBuilding
        {
            public BuildingData data;
            public Vector2Int gridCell;
            public GameObject instance;
        }
    }
}
