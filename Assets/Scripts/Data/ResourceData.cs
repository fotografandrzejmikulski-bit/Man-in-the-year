using UnityEngine;

namespace ManInTheYear.Data
{
    /// <summary>
    /// ScriptableObject that defines a single resource type (e.g. Wood, Stone, Gold).
    /// Lives in Assets/ScriptableObjects/Resources and is edited by designers in the Inspector.
    /// Keeps pure data – no runtime state – so instances can be safely shared read-only.
    /// </summary>
    [CreateAssetMenu(fileName = "NewResource", menuName = "Man In The Year/Resource Data")]
    public class ResourceData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique string key used in save-data and lookups (e.g. \"wood\").")]
        public string resourceId;

        [Tooltip("Human-readable display name shown in the UI.")]
        public string displayName;

        [Tooltip("Icon shown in the HUD inventory strip.")]
        public Sprite icon;

        [Header("Gathering")]
        [Tooltip("Base amount yielded per gather interaction.")]
        [Min(1)]
        public int baseYieldAmount = 1;

        [Tooltip("Seconds between consecutive gathers from the same node.")]
        [Min(0f)]
        public float gatherCooldownSeconds = 1f;

        [Header("Limits")]
        [Tooltip("Maximum units the player can carry at once (0 = unlimited).")]
        [Min(0)]
        public int maxCarryCapacity = 0;
    }
}
