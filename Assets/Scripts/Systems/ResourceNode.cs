using UnityEngine;
using ManInTheYear.Data;
using ManInTheYear.Pooling;

namespace ManInTheYear.Systems
{
    /// <summary>
    /// Placed on every harvestable object in the world (tree, rock, ore vein …).
    ///
    /// Responsibilities
    /// ────────────────
    /// • Owns the per-node cooldown timer and remaining uses.
    /// • Spawns a pickup VFX/particle from the shared ObjectPool (zero allocations).
    /// • Delegates ALL inventory mutation to ResourceSystem.
    ///
    /// Level-Designer integration
    /// ──────────────────────────
    /// Drag the desired ResourceData ScriptableObject onto the [resource] slot.
    /// Tweak [overrideYield] and [maxUses] per node if needed.
    /// The node deactivates itself automatically when depleted.
    ///
    /// Mobile optimisation
    /// ───────────────────
    /// • Uses a single SphereCollider trigger – no Rigidbody sweep cost.
    /// • The VFX prefab must use a Particle System with GPU instancing enabled.
    /// • The renderer uses a shared Material so batching is preserved.
    /// </summary>
    public sealed class ResourceNode : MonoBehaviour
    {
        [Header("Resource Definition")]
        [Tooltip("Which resource this node yields. Assign a ResourceData ScriptableObject.")]
        [SerializeField] private ResourceData resource;

        [Header("Override (0 = use ResourceData defaults)")]
        [Tooltip("Override yield amount per gather (0 = use ResourceData.baseYieldAmount).")]
        [SerializeField, Min(0)] private int overrideYield = 0;

        [Tooltip("How many times this node can be gathered before depletion (0 = infinite).")]
        [SerializeField, Min(0)] private int maxUses = 3;

        [Header("Feedback")]
        [Tooltip("VFX prefab spawned via ObjectPool when gathered.")]
        [SerializeField] private GameObject gatherVfxPrefab;

        // ── Runtime state ─────────────────────────────────────────────────────────
        private int _remainingUses;
        private float _cooldownTimer;
        private bool _onCooldown;

        // ── Unity lifecycle ───────────────────────────────────────────────────────
        private void Start()
        {
            _remainingUses = maxUses;
        }

        private void Update()
        {
            if (!_onCooldown) return;

            _cooldownTimer -= Time.deltaTime;
            if (_cooldownTimer <= 0f)
                _onCooldown = false;
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Called by the touch/interaction system when the player taps this node.
        /// Returns true when a gather actually happened.
        /// </summary>
        public bool TryGather()
        {
            if (resource == null) return false;
            if (_onCooldown) return false;
            if (maxUses > 0 && _remainingUses <= 0) return false;

            int yield = overrideYield > 0 ? overrideYield : resource.baseYieldAmount;
            ResourceSystem.Instance.TryAdd(resource, yield);

            // VFX via pool – avoids Instantiate/Destroy cost
            if (gatherVfxPrefab != null)
                ObjectPool.Instance.Spawn(gatherVfxPrefab, transform.position, Quaternion.identity);

            _onCooldown = true;
            _cooldownTimer = resource.gatherCooldownSeconds;

            if (maxUses > 0)
            {
                _remainingUses--;
                if (_remainingUses <= 0)
                    Deplete();
            }

            return true;
        }

        // ── Private helpers ───────────────────────────────────────────────────────
        private void Deplete()
        {
            // Disable the GameObject; a world-manager can re-enable it after a respawn timer.
            gameObject.SetActive(false);
        }
    }
}
