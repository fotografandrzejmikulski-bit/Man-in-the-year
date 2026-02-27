using System.Collections.Generic;
using UnityEngine;

namespace ManInTheYear.Pooling
{
    /// <summary>
    /// Generic, single-shared-instance object pool.
    ///
    /// Why this matters for mobile
    /// ───────────────────────────
    /// • Instantiate() and Destroy() trigger garbage-collection pressure and Draw Call
    ///   spikes.  Pooling eliminates both.
    /// • VFX particle systems, damage numbers, and pickup icons are the typical hot paths
    ///   on a resource-gathering game.
    ///
    /// Usage
    /// ─────
    ///   ObjectPool.Instance.Spawn(myPrefab, position, rotation);   // get from pool
    ///   ObjectPool.Instance.Return(myGameObject);                  // return to pool
    ///
    /// The pool auto-returns objects after <see cref="autoReturnAfterSeconds"/> if the
    /// spawned prefab has an AutoReturn component attached.
    ///
    /// Mobile optimisation notes
    /// ─────────────────────────
    /// GPU: Pooled Particle Systems with GPU instancing cost ~1 Draw Call for the whole
    ///      active set if they share a material.
    /// RAM: Pre-warm the pool at scene start (call Prewarm()) to avoid allocation
    ///      during gameplay.
    /// </summary>
    public sealed class ObjectPool : MonoBehaviour
    {
        // ── Singleton ────────────────────────────────────────────────────────────
        public static ObjectPool Instance { get; private set; }

        [Tooltip("Default lifetime of pooled objects when no AutoReturn component is present (seconds, 0 = manual return).")]
        [SerializeField, Min(0f)] private float autoReturnAfterSeconds = 3f;

        // ── Pool storage ──────────────────────────────────────────────────────────
        // Key: the source prefab (not instance).
        private readonly Dictionary<GameObject, Stack<GameObject>> _pool
            = new Dictionary<GameObject, Stack<GameObject>>(16);

        // Maps each spawned instance back to its source prefab for correct Return().
        private readonly Dictionary<GameObject, GameObject> _instanceToPrefab
            = new Dictionary<GameObject, GameObject>(64);

        // ── Unity lifecycle ───────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Pre-warms the pool with <paramref name="count"/> instances of <paramref name="prefab"/>.
        /// Call from a loading screen / scene initialiser to avoid mid-game allocations.
        /// </summary>
        public void Prewarm(GameObject prefab, int count)
        {
            for (int i = 0; i < count; i++)
            {
                GameObject go = CreateInstance(prefab);
                go.SetActive(false);
                Push(prefab, go);
            }
        }

        /// <summary>
        /// Returns an active, positioned instance of <paramref name="prefab"/>.
        /// Creates a new instance only if the pool is empty.
        /// </summary>
        public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            GameObject go = Pop(prefab) ?? CreateInstance(prefab);

            go.transform.SetPositionAndRotation(position, rotation);
            go.SetActive(true);

            if (autoReturnAfterSeconds > 0f)
                StartCoroutine(AutoReturnRoutine(prefab, go, autoReturnAfterSeconds));

            return go;
        }

        /// <summary>Deactivates <paramref name="instance"/> and returns it to the pool.</summary>
        public void Return(GameObject instance)
        {
            if (instance == null) return;
            instance.SetActive(false);
            instance.transform.SetParent(transform);

            if (_instanceToPrefab.TryGetValue(instance, out GameObject prefab))
                Push(prefab, instance);
        }

        // ── Private helpers ───────────────────────────────────────────────────────
        private GameObject CreateInstance(GameObject prefab)
        {
            GameObject go = Instantiate(prefab, transform);
            go.SetActive(false);
            _instanceToPrefab[go] = prefab;   // register mapping for Return()
            return go;
        }

        private GameObject Pop(GameObject prefab)
        {
            if (_pool.TryGetValue(prefab, out Stack<GameObject> stack) && stack.Count > 0)
                return stack.Pop();
            return null;
        }

        private void Push(GameObject prefab, GameObject instance)
        {
            if (!_pool.TryGetValue(prefab, out Stack<GameObject> stack))
            {
                stack = new Stack<GameObject>(8);
                _pool[prefab] = stack;
            }
            stack.Push(instance);
        }

        private System.Collections.IEnumerator AutoReturnRoutine(
            GameObject prefab, GameObject instance, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (instance != null && instance.activeSelf)
            {
                instance.SetActive(false);
                instance.transform.SetParent(transform);
                Push(prefab, instance);
            }
        }
    }
}
