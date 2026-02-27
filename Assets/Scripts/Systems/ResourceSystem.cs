using System.Collections.Generic;
using UnityEngine;
using ManInTheYear.Data;

namespace ManInTheYear.Systems
{
    /// <summary>
    /// Central runtime data store for the player's resource inventory.
    ///
    /// Design goals
    /// ────────────
    /// • Data-oriented: stores only plain integers keyed by ResourceData asset references.
    ///   No MonoBehaviour responsibilities – can be unit-tested without a running scene.
    /// • Event-driven: fires C# events so the UI layer never polls this class.
    /// • Mobile-friendly: no per-frame allocations; Dictionary is pre-warmed at startup.
    ///
    /// Mobile optimisation notes
    /// ─────────────────────────
    /// CPU: Avoid LINQ in hot paths (gathering tick).  Use plain for-loops.
    /// RAM: Keep the inventory dictionary small; enforce maxCarryCapacity at write time.
    /// </summary>
    public sealed class ResourceSystem : MonoBehaviour
    {
        // ── Singleton ────────────────────────────────────────────────────────────
        public static ResourceSystem Instance { get; private set; }

        // ── Events ───────────────────────────────────────────────────────────────
        /// <summary>Raised after any resource amount changes. (resource, newAmount)</summary>
        public event System.Action<ResourceData, int> OnResourceChanged;

        // ── State (private, no public fields that could be mutated externally) ───
        private readonly Dictionary<ResourceData, int> _inventory = new Dictionary<ResourceData, int>(16);

        // ── Unity lifecycle ───────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>Returns the current amount of <paramref name="resource"/> the player holds.</summary>
        public int GetAmount(ResourceData resource)
        {
            _inventory.TryGetValue(resource, out int amount);
            return amount;
        }

        /// <summary>
        /// Attempts to add <paramref name="delta"/> units of <paramref name="resource"/>.
        /// Respects maxCarryCapacity (0 = unlimited).
        /// Returns the actual amount added (may be less than requested if at capacity).
        /// </summary>
        public int TryAdd(ResourceData resource, int delta)
        {
            if (resource == null || delta <= 0) return 0;

            _inventory.TryGetValue(resource, out int current);

            int allowed = delta;
            if (resource.maxCarryCapacity > 0)
            {
                int headroom = resource.maxCarryCapacity - current;
                if (headroom <= 0) return 0;
                allowed = Mathf.Min(delta, headroom);
            }

            _inventory[resource] = current + allowed;
            OnResourceChanged?.Invoke(resource, _inventory[resource]);
            return allowed;
        }

        /// <summary>
        /// Deducts <paramref name="amount"/> of <paramref name="resource"/> if available.
        /// Returns true on success, false if insufficient funds.
        /// </summary>
        public bool TrySpend(ResourceData resource, int amount)
        {
            if (resource == null || amount <= 0) return false;

            _inventory.TryGetValue(resource, out int current);
            if (current < amount) return false;

            _inventory[resource] = current - amount;
            OnResourceChanged?.Invoke(resource, _inventory[resource]);
            return true;
        }

        /// <summary>
        /// Checks whether all entries in <paramref name="costs"/> can be satisfied.
        /// Pure read – no side effects.
        /// </summary>
        public bool CanAfford(IReadOnlyList<ResourceCost> costs)
        {
            // Plain for-loop – no LINQ allocation on mobile.
            for (int i = 0; i < costs.Count; i++)
            {
                if (GetAmount(costs[i].resource) < costs[i].amount)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Atomically deducts all <paramref name="costs"/>.
        /// Returns false (and deducts nothing) if any cost cannot be met.
        /// </summary>
        public bool TrySpendAll(IReadOnlyList<ResourceCost> costs)
        {
            if (!CanAfford(costs)) return false;

            for (int i = 0; i < costs.Count; i++)
                TrySpend(costs[i].resource, costs[i].amount);

            return true;
        }
    }
}
