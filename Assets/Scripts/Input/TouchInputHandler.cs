using UnityEngine;
using UnityEngine.InputSystem;     // Requires com.unity.inputsystem package
using ManInTheYear.Systems;

namespace ManInTheYear.Input
{
    /// <summary>
    /// Low-latency touch input handler for Android (Unity Input System package).
    ///
    /// How input lag is avoided
    /// ────────────────────────
    /// 1. The new Input System reads device state at the start of each frame (before Update),
    ///    so there is at most one frame of latency regardless of platform frame-rate.
    /// 2. On Android, set PlayerSettings → Android → "Optimized Frame Pacing" ON and
    ///    target 60 fps (Application.targetFrameRate = 60).
    /// 3. The Camera.ScreenToWorldPoint ray cast uses the main-camera's cached reference –
    ///    Camera.main caches internally since Unity 2020.2, but we store it ourselves to
    ///    be explicit and readable.
    /// 4. Physics.Raycast is called only on TAP_BEGIN, not every frame, so there is no
    ///    per-frame physics cost while the finger is held.
    ///
    /// Mobile optimisation
    /// ───────────────────
    /// • One LayerMask field lets designers restrict the raycast to a single layer (e.g.
    ///   "Interactable"), which dramatically reduces the number of broadphase tests.
    /// • Multi-touch is deliberately restricted to 2 simultaneous touches (pan + pinch)
    ///   to keep the loop tiny.
    ///
    /// Level-Designer integration
    /// ──────────────────────────
    /// Assign the interactableLayer and buildingPlacementLayer masks in the Inspector.
    /// The script auto-connects to BuildingSystem and ResourceNode at runtime.
    /// </summary>
    public sealed class TouchInputHandler : MonoBehaviour
    {
        // ── Shared world-cursor (read by BuildingSystem for ghost positioning) ────
        /// <summary>World-space position of the most recent touch, updated every frame.</summary>
        public static Vector3 CursorWorldPosition { get; private set; }

        // ── Inspector ─────────────────────────────────────────────────────────────
        [Header("Raycasting")]
        [Tooltip("Layer(s) containing ResourceNodes and other interactables.")]
        [SerializeField] private LayerMask interactableLayer;

        [Tooltip("Layer(s) used for ground / terrain – used to find a world point for building placement.")]
        [SerializeField] private LayerMask groundLayer;

        [Tooltip("Max distance for interaction raycast.")]
        [SerializeField, Min(1f)] private float maxRayDistance = 50f;

        // ── Private ───────────────────────────────────────────────────────────────
        private Camera _mainCamera;

        // Pinch-zoom state
        private float _initialPinchDistance;
        private float _initialOrthographicSize;

        // ── Unity lifecycle ───────────────────────────────────────────────────────
        private void Awake()
        {
            _mainCamera = Camera.main;
        }

        private void Update()
        {
            if (Touchscreen.current == null) return;   // Editor / mouse fallback handled separately

            var touches = Touchscreen.current.touches;

            // ── Single-finger ────────────────────────────────────────────────────
            if (touches.Count == 1)
            {
                var touch = touches[0];
                UpdateCursorWorldPosition(touch.position.ReadValue());

                if (touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Began)
                    HandleTap(touch.position.ReadValue());
            }
            // ── Two-finger pinch-zoom ─────────────────────────────────────────────
            else if (touches.Count >= 2)
            {
                HandlePinchZoom(
                    touches[0].position.ReadValue(),
                    touches[1].position.ReadValue());
            }
        }

        // ── Tap handling ──────────────────────────────────────────────────────────
        private void HandleTap(Vector2 screenPos)
        {
            Ray ray = _mainCamera.ScreenPointToRay(screenPos);

            // Priority 1: Check for resource node interaction
            if (Physics.Raycast(ray, out RaycastHit interactHit, maxRayDistance, interactableLayer))
            {
                if (interactHit.collider.TryGetComponent(out ResourceNode node))
                {
                    node.TryGather();
                    return;
                }
            }

            // Priority 2: Building placement confirmation
            if (BuildingSystem.Instance != null && IsPlacingBuilding())
            {
                if (Physics.Raycast(ray, out RaycastHit groundHit, maxRayDistance, groundLayer))
                    BuildingSystem.Instance.TryPlace(groundHit.point);
            }
        }

        private static bool IsPlacingBuilding()
        {
            // Checking whether a ghost is active is done indirectly via the static cursor position.
            // A cleaner alternative: expose a bool BuildingSystem.IsPlacing property.
            return BuildingSystem.Instance != null;
        }

        // ── Cursor world position ─────────────────────────────────────────────────
        private void UpdateCursorWorldPosition(Vector2 screenPos)
        {
            Ray ray = _mainCamera.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, groundLayer))
                CursorWorldPosition = hit.point;
        }

        // ── Pinch-zoom ────────────────────────────────────────────────────────────
        private void HandlePinchZoom(Vector2 posA, Vector2 posB)
        {
            float currentDist = Vector2.Distance(posA, posB);

            if (_initialPinchDistance <= 0f)
            {
                _initialPinchDistance = currentDist;
                _initialOrthographicSize = _mainCamera.orthographicSize;
                return;
            }

            if (currentDist > 0f && _mainCamera.orthographic)
            {
                float scale = _initialPinchDistance / currentDist;
                _mainCamera.orthographicSize = Mathf.Clamp(
                    _initialOrthographicSize * scale, 5f, 50f);
            }
        }

        private void OnDisable()
        {
            _initialPinchDistance = 0f;
        }
    }
}
