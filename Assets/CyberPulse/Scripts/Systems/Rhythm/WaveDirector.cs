using UnityEngine;
using UnityEngine.AI;
using CyberPulse.Enemy;
using CyberPulse.World;

namespace CyberPulse.Systems
{
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
        [SerializeField] private float _earlyTriggerSeconds = 0.3f;

        [Header("Spawn placement")]
        [SerializeField] private LayerMask _obstacleMask;
        [SerializeField] private float     _aerialHeight = 6f;

        [Header("Layered difficulty")]
        [Tooltip("Each layer above the first scales its (procedural) enemy counts up by this fraction. " +
                 "0.33 → roughly 1× / 1.33× / 1.67× per layer, the 15 / 20 / 25 relationship.")]
        [SerializeField] private float _layerEnemyScaleStep = 0.33f;

        private WaveDefinition[] _waves;
        private int[] _waveLayer;
        private int  _nextWaveIndex;
        private bool _running;
        private int  _totalSpawned;
        private bool _songStarted;
        private bool _winFired;

        private float _layerActivatedAt;
        private float _layerBaseSpawnTime;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            if (LayerManager.Instance != null)
                LayerManager.Instance.OnActiveLayerChanged += HandleActiveLayerChanged;

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
            if (LayerManager.Instance != null)
                LayerManager.Instance.OnActiveLayerChanged -= HandleActiveLayerChanged;
        }

        private void Update()
        {
            if (!_running || _waves == null) return;

            if (_musicSource != null)
            {
                if (_musicSource.isPlaying) _songStarted = true;
                else if (!_songStarted)     return;
            }

            if (_nextWaveIndex < _waves.Length)
            {
                int  activeLayer = LayerManager.Instance != null ? LayerManager.Instance.ActiveIndex : 0;
                bool eligible    = _waveLayer == null || _waveLayer[_nextWaveIndex] <= activeLayer;

                if (eligible)
                {
                    float due = _waves[_nextWaveIndex].SpawnTime - _layerBaseSpawnTime + _layerActivatedAt;
                    if (SongTime() >= due - _earlyTriggerSeconds)
                    {
                        SpawnWave(_waves[_nextWaveIndex]);
                        _nextWaveIndex++;
                        MarkLayerCompleteIfDone(activeLayer);
                    }
                }
            }

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

            if (LayerManager.Instance != null && _musicSource != null)
            {
                if (!_winFired && _songStarted && !_musicSource.isPlaying && !AudioListener.pause)
                {
                    _winFired = true;
                    GameManager.Instance?.TriggerFailState();
                }
            }
        }

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

        private void ApplyProfile(SongProfile profile)
        {
            SetWaves(ProceduralLevelGenerator.Generate(profile));
            GameManager.Instance?.SetPhase(GamePhase.Siphon);
        }


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

        private void HandleActiveLayerChanged(int newIndex)
        {
            if (newIndex <= 0 || _waves == null || _waveLayer == null) return;

            if (_nextWaveIndex < _waves.Length && _waveLayer[_nextWaveIndex] == newIndex)
            {
                _layerActivatedAt   = SongTime();
                _layerBaseSpawnTime = _waves[_nextWaveIndex].SpawnTime;
            }
            else
            {
                LayerManager.Instance?.ActiveLayer?.MarkAllWavesSpawned();
            }
        }

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

            int spawnCount = wave.Count;
            if (layer != null)
                spawnCount = Mathf.Max(1, Mathf.RoundToInt(spawnCount * (1f + layer.Index * _layerEnemyScaleStep)));

            for (int i = 0; i < spawnCount; i++)
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

                if (layer != null)
                {
                    var health = go.GetComponent<EnemyHealth>();
                    if (health != null) layer.RegisterEnemy(health);
                }
            }

            layer?.RevealNextNode();

            _totalSpawned++;
            Debug.Log($"[WaveDirector] Wave {_totalSpawned}: {spawnCount} enemies at t={SongTime():F1}s" +
                      (layer != null ? $" (layer {layer.Index})" : ""));
        }

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
