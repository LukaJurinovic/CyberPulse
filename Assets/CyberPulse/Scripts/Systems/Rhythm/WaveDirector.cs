using UnityEngine;
using UnityEngine.AI;
using CyberPulse.Enemy;
using CyberPulse.World;

namespace CyberPulse.Systems
{
    /// <summary>
    /// Runtime wave spawner. Reads WaveDefinition[] (from ProceduralLevelGenerator
    /// via SongAnalyzer) and instantiates enemy prefabs when the song time reaches
    /// each wave's SpawnTime. Uses timeSamples / outputSampleRate for drift-free
    /// timing (plan.md §6).
    ///
    /// _seekerPrefab must be wired by PlayableLevelBuilder. Until then, WaveDirector
    /// logs a warning and skips spawning silently — the static scene enemies remain.
    /// </summary>
    public class WaveDirector : MonoBehaviour
    {
        public static WaveDirector Instance { get; private set; }

        [Header("References")]
        [SerializeField] private AudioSource _musicSource;
        [SerializeField] private GameObject  _seekerPrefab;
        [SerializeField] private GameObject  _spherePrefab;
        [SerializeField] private GameObject  _trianglePrefab;
        [SerializeField] private GameObject  _cylinderPrefab;
        [SerializeField] private GameObject  _cubePrefab;

        [Header("Timing")]
        [SerializeField] private float _earlyTriggerSeconds = 0.3f;  // spawn slightly ahead of marked time

        [Header("Spawn placement")]
        [SerializeField] private LayerMask _obstacleMask;            // arena geometry — aerial spawns avoid overlapping it
        [SerializeField] private float     _aerialHeight = 6f;

        private WaveDefinition[] _waves;
        private int[] _waveLayer;     // per-wave layer index (layered mode); null = single-arena
        private int  _nextWaveIndex;
        private bool _running;
        private int  _totalSpawned;
        private bool _songStarted;
        private bool _winFired;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            if (SongAnalyzer.Instance == null) return;

            if (SongAnalyzer.Instance.IsAnalyzed)
                ApplyProfile(SongAnalyzer.Instance.Profile);
            else
                SongAnalyzer.Instance.OnAnalysisComplete += ApplyProfile;
        }

        private void OnDestroy()
        {
            if (SongAnalyzer.Instance != null)
                SongAnalyzer.Instance.OnAnalysisComplete -= ApplyProfile;
        }

        private void Update()
        {
            if (!_running || _waves == null) return;

            // Wait for the song to actually begin before running the timeline. The
            // loading screen holds playback until analysis finishes, so scheduling off
            // SongTime()'s Time.time fallback would fire waves during the load screen.
            if (_musicSource != null)
            {
                if (_musicSource.isPlaying) _songStarted = true;
                else if (!_songStarted)     return;
            }

            if (_nextWaveIndex < _waves.Length)
            {
                // Layered mode: hold any wave bound to a layer the player hasn't reached yet,
                // so each stacked arena only fights its own segment of the timeline.
                int  activeLayer = LayerManager.Instance != null ? LayerManager.Instance.ActiveIndex : 0;
                bool eligible    = _waveLayer == null || _waveLayer[_nextWaveIndex] <= activeLayer;

                if (eligible)
                {
                    float t = SongTime();
                    if (t >= _waves[_nextWaveIndex].SpawnTime - _earlyTriggerSeconds)
                    {
                        SpawnWave(_waves[_nextWaveIndex]);
                        _nextWaveIndex++;
                        MarkLayerCompleteIfDone(activeLayer);
                    }
                }
            }

            // Win condition. Layered mode wins via LayerManager when the top layer clears, so
            // only the single-arena fallback uses the song-end + no-enemies check here.
            if (LayerManager.Instance == null && _musicSource != null)
            {
                if (!_winFired && _songStarted && !_musicSource.isPlaying && !AudioListener.pause
                    && _nextWaveIndex >= _waves.Length
                    && TraceMeter.Instance?.EnemyCount == 0)
                {
                    _winFired = true;
                    GameManager.Instance?.TriggerWinState();
                }
            }

            // Fail condition for layered mode: song ends before the player cleared all layers.
            // LayerManager owns the win; if it hasn't fired by the time the track runs out, it's a loss.
            if (LayerManager.Instance != null && _musicSource != null)
            {
                if (!_winFired && _songStarted && !_musicSource.isPlaying && !AudioListener.pause)
                {
                    _winFired = true;
                    GameManager.Instance?.TriggerFailState();
                }
            }
        }

        // ── Public API ────────────────────────────────────────────────────────

        public bool AllWavesSent => _waves != null && _nextWaveIndex >= _waves.Length;
        public int  WaveIndex    => _nextWaveIndex;
        public int  WaveCount    => _waves?.Length ?? 0;

        public void SetMusicSource(AudioSource src)
        {
            _musicSource = src;
        }

        public void SetWaves(WaveDefinition[] waves)
        {
            _waves          = waves;
            _nextWaveIndex  = 0;
            _running        = true;
            AssignWaveLayers();
        }

        // ── Private ───────────────────────────────────────────────────────────

        private void ApplyProfile(SongProfile profile)
        {
            SetWaves(ProceduralLevelGenerator.Generate(profile));
            GameManager.Instance?.SetPhase(GamePhase.Siphon);
        }

        /// <summary>
        /// Splits the wave timeline evenly across the LayerManager's stacked arenas so each
        /// layer fights its own segment. Set to null in single-arena mode (no gating).
        /// </summary>
        private void AssignWaveLayers()
        {
            int layerCount = LayerManager.Instance != null ? LayerManager.Instance.LayerCount : 0;
            if (layerCount <= 1 || _waves == null || _waves.Length == 0)
            {
                _waveLayer = null;
                return;
            }

            _waveLayer = new int[_waves.Length];
            for (int i = 0; i < _waves.Length; i++)
                _waveLayer[i] = Mathf.Min(i * layerCount / _waves.Length, layerCount - 1);
        }

        /// <summary>
        /// Once the next pending wave belongs to a higher layer (or none remain), the active
        /// layer has received every wave it ever will — tell it so it can become clearable.
        /// </summary>
        private void MarkLayerCompleteIfDone(int activeLayer)
        {
            var layer = LayerManager.Instance?.ActiveLayer;
            if (layer == null) return;

            bool noneLeftForLayer = _nextWaveIndex >= _waves.Length
                                 || (_waveLayer != null && _waveLayer[_nextWaveIndex] > activeLayer);
            if (noneLeftForLayer)
                layer.MarkAllWavesSpawned();
        }

        private void SpawnWave(WaveDefinition wave)
        {
            var layer = LayerManager.Instance != null ? LayerManager.Instance.ActiveLayer : null;

            for (int i = 0; i < wave.Count; i++)
            {
                EnemyType  type   = wave.EnemyTypes[i % wave.EnemyTypes.Length];
                GameObject prefab = PrefabForType(type);

                if (prefab == null)
                {
                    Debug.LogWarning($"[WaveDirector] No prefab wired for {type} — skipping enemy.");
                    continue;
                }

                Vector3 pos = layer != null
                    ? ResolveLayerSpawn(layer, type)
                    : ResolveSpawnPosition(
                          i < wave.SpawnPositions.Length
                              ? wave.SpawnPositions[i]
                              : new Vector3(Random.Range(-22f, 22f), 0f, Random.Range(-22f, 22f)),
                          type);

                var go = Instantiate(prefab, pos, Quaternion.identity);

                // Layered mode: hand the enemy to its layer so its death counts toward the clear.
                if (layer != null)
                {
                    var health = go.GetComponent<EnemyHealth>();
                    if (health != null) layer.RegisterEnemy(health);
                }
            }

            // Progressive reveal: each wave uncovers one more objective node in the layer.
            layer?.RevealNextNode();

            _totalSpawned++;
            Debug.Log($"[WaveDirector] Wave {_totalSpawned}: {wave.Count} enemies at t={SongTime():F1}s" +
                      (layer != null ? $" (layer {layer.Index})" : ""));
        }

        /// <summary>Spawn position inside a specific arena layer; ground units snap to its NavMesh.</summary>
        private Vector3 ResolveLayerSpawn(ArenaLayer layer, EnemyType type)
        {
            if (type == EnemyType.SphereAerial)
            {
                Vector3 air = layer.RandomSpawnPoint();
                air.y = layer.FlightHeight;
                return air;
            }

            Vector3 ground = layer.RandomSpawnPoint();
            if (NavMesh.SamplePosition(ground, out var hit, 6f, NavMesh.AllAreas))
                return hit.position;
            return ground;
        }

        /// <summary>
        /// Keeps spawns out of walls. Grounded enemies snap to the NavMesh (carved cover
        /// is excluded, so a position inside a block resolves to the nearest open floor).
        /// Aerial enemies keep their height but jitter off any geometry they'd overlap.
        /// </summary>
        private Vector3 ResolveSpawnPosition(Vector3 desired, EnemyType type)
        {
            bool aerial = type == EnemyType.SphereAerial;

            if (!aerial)
            {
                desired.y = 0f;
                if (NavMesh.SamplePosition(desired, out var navHit, 6f, NavMesh.AllAreas))
                    return navHit.position;
                return desired;
            }

            // Aerial: find a clear point at flight height.
            for (int attempt = 0; attempt < 8; attempt++)
            {
                Vector3 candidate = desired;
                if (attempt > 0)
                {
                    Vector2 j = Random.insideUnitCircle * 5f;
                    candidate += new Vector3(j.x, 0f, j.y);
                }
                candidate.y = _aerialHeight;

                if (_obstacleMask.value == 0 ||
                    !Physics.CheckSphere(candidate, 1f, _obstacleMask, QueryTriggerInteraction.Ignore))
                    return candidate;
            }
            desired.y = _aerialHeight;
            return desired;
        }

        private GameObject PrefabForType(EnemyType type) => type switch
        {
            EnemyType.Seeker          => _seekerPrefab,
            EnemyType.SphereAerial    => _spherePrefab,
            EnemyType.TriangleMirror  => _trianglePrefab,
            EnemyType.CylinderLauncher => _cylinderPrefab,
            EnemyType.CubeSplitter    => _cubePrefab,
            _                         => null,
        };

        private float SongTime()
        {
            if (_musicSource != null && _musicSource.isPlaying)
                return (float)_musicSource.timeSamples / AudioSettings.outputSampleRate;
            return Time.time;
        }
    }
}
