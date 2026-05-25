using System.Collections;
using UnityEngine;
using CyberPulse.Systems;

namespace CyberPulse.Enemy
{
    /// <summary>
    /// Spawns enemies during the Purge phase and advances to Extract when all are dead.
    /// Listens to GameManager.OnPhaseChanged; also exposes StartSpawning() for manual trigger.
    /// Enable _autoStart to test without a data-node system in place.
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        [Header("Prefabs")]
        [SerializeField] private GameObject[] _enemyPrefabs;

        [Header("Spawn Points")]
        [SerializeField] private Transform[] _spawnPoints;

        [Header("Timing")]
        [SerializeField] private float _initialDelay  = 1f;
        [SerializeField] private float _spawnInterval = 4f;

        [Header("Limits")]
        [SerializeField] private int _maxLiveEnemies = 6;
        [SerializeField] private int _totalToSpawn   = 20; // 0 = unlimited

        [Header("Debug")]
        [SerializeField] private bool _autoStart = false;

        private int _spawnedCount;
        private int _liveCount;
        private bool _running;
        private Coroutine _spawnRoutine;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Start()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnPhaseChanged += OnPhaseChanged;
            if (_autoStart) StartSpawning();
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnPhaseChanged -= OnPhaseChanged;
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void StartSpawning()
        {
            if (_running) return;
            _running = true;
            _spawnRoutine = StartCoroutine(SpawnLoop());
        }

        public void StopSpawning()
        {
            _running = false;
            if (_spawnRoutine != null) StopCoroutine(_spawnRoutine);
            _spawnRoutine = null;
        }

        // ── Phase listener ────────────────────────────────────────────────────

        private void OnPhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.Purge)  StartSpawning();
            else                           StopSpawning();
        }

        // ── Spawn loop ────────────────────────────────────────────────────────

        private IEnumerator SpawnLoop()
        {
            yield return new WaitForSeconds(_initialDelay);

            while (_running)
            {
                bool limitReached = _totalToSpawn > 0 && _spawnedCount >= _totalToSpawn;

                // All enemies from this wave dead → advance to Extract.
                if (limitReached && _liveCount == 0)
                {
                    GameManager.Instance?.SetPhase(GamePhase.Extract);
                    yield break;
                }

                bool prefabsReady  = _enemyPrefabs  != null && _enemyPrefabs.Length  > 0;
                bool pointsReady   = _spawnPoints    != null && _spawnPoints.Length   > 0;
                bool underCap      = _liveCount < _maxLiveEnemies;

                if (!limitReached && prefabsReady && pointsReady && underCap)
                    SpawnOne();

                yield return new WaitForSeconds(_spawnInterval);
            }
        }

        private void SpawnOne()
        {
            var prefab = _enemyPrefabs[Random.Range(0, _enemyPrefabs.Length)];
            var point  = _spawnPoints[Random.Range(0, _spawnPoints.Length)];
            if (prefab == null || point == null) return;

            var go     = Instantiate(prefab, point.position, point.rotation);
            _spawnedCount++;
            _liveCount++;

            var health = go.GetComponent<EnemyHealth>();
            if (health != null)
                health.OnDeath += () => _liveCount = Mathf.Max(0, _liveCount - 1);
        }

        // ── Gizmos ────────────────────────────────────────────────────────────

        private void OnDrawGizmosSelected()
        {
            if (_spawnPoints == null) return;
            Gizmos.color = new Color(1f, 0.3f, 0f, 0.9f);
            foreach (var pt in _spawnPoints)
                if (pt != null) Gizmos.DrawWireSphere(pt.position, 0.6f);
        }
    }
}
