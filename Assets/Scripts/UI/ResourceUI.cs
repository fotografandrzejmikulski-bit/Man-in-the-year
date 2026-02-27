using UnityEngine;
using UnityEngine.UI;
using TMPro;                         // TextMeshPro – standard in new Unity projects
using ManInTheYear.Data;
using ManInTheYear.Systems;

namespace ManInTheYear.UI
{
    /// <summary>
    /// HUD widget that displays a single resource amount (icon + count text).
    ///
    /// Presentation layer – never touches inventory data directly.
    /// It subscribes to <see cref="ResourceSystem.OnResourceChanged"/> so the display
    /// is always reactive and there is NO per-frame polling cost.
    ///
    /// Level-Designer integration
    /// ──────────────────────────
    /// Drag the ResourceData asset that this widget should track onto the [trackedResource]
    /// field.  The widget connects itself to ResourceSystem at runtime.
    ///
    /// Mobile optimisation
    /// ───────────────────
    /// • UI uses Canvas "Screen Space – Overlay" with a single Canvas per panel to
    ///   minimise layout rebuild cost.
    /// • Text is updated only when the value changes (event-driven), not every frame.
    /// • Image.sprite assignment is done once in Start; no per-frame texture swap.
    /// </summary>
    public sealed class ResourceUI : MonoBehaviour
    {
        [Header("Data Binding")]
        [Tooltip("The resource this widget represents. Assign a ResourceData ScriptableObject.")]
        [SerializeField] private ResourceData trackedResource;

        [Header("UI References")]
        [SerializeField] private Image resourceIcon;
        [SerializeField] private TMP_Text amountText;

        // ── Unity lifecycle ───────────────────────────────────────────────────────
        private void Start()
        {
            if (trackedResource == null)
            {
                Debug.LogWarning($"[ResourceUI] {name}: trackedResource is not assigned.", this);
                return;
            }

            // Bind icon once
            if (resourceIcon != null)
                resourceIcon.sprite = trackedResource.icon;

            // Refresh display immediately with current value
            RefreshDisplay(trackedResource, ResourceSystem.Instance.GetAmount(trackedResource));

            // Subscribe to future changes
            ResourceSystem.Instance.OnResourceChanged += OnResourceChanged;
        }

        private void OnDestroy()
        {
            if (ResourceSystem.Instance != null)
                ResourceSystem.Instance.OnResourceChanged -= OnResourceChanged;
        }

        // ── Event handler ─────────────────────────────────────────────────────────
        private void OnResourceChanged(ResourceData resource, int newAmount)
        {
            // Only redraw when OUR resource changed – avoids unnecessary string allocs.
            if (resource == trackedResource)
                RefreshDisplay(resource, newAmount);
        }

        // ── Display update ────────────────────────────────────────────────────────
        private void RefreshDisplay(ResourceData resource, int amount)
        {
            if (amountText == null) return;

            // String format with capacity limit hint for designer readability.
            if (resource.maxCarryCapacity > 0)
                amountText.text = $"{amount}/{resource.maxCarryCapacity}";
            else
                amountText.text = amount.ToString();
        }
    }
}
