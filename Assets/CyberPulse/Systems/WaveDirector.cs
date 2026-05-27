using UnityEngine;

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

        private WaveDefinition[] _waves;
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

            if (_nextWaveIndex < _waves.Length)
            {
                float t = SongTime();
                if (t >= _waves[_nextWaveIndex].SpawnTime - _earlyTriggerSeconds)
                    SpawnWave(_waves[_nextWaveIndex++]);
            }

            // Song-end win condition — replaces removed exit trigger
            if (_musicSource != null)
            {
                if (_musicSource.isPlaying) _songStarted = true;

                if (!_winFired && _songStarted && !_musicSource.isPlaying
                    && _nextWaveIndex >= _waves.Length)
                {
                    _winFired = true;
                    GameManager.Instance?.TriggerWinState();
                }
            }
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void SetMusicSource(AudioSource src)
        {
            _musicSource = src;
        }

        public void SetWaves(WaveDefinition[] waves)
        {
            _waves          = waves;
            _nextWaveIndex  = 0;
            _running        = true;
        }

        // ── Private ───────────────────────────────────────────────────────────

        private void ApplyProfile(SongProfile profile)
        {
            SetWaves(ProceduralLevelGenerator.Generate(profile));
        }

        private void SpawnWave(WaveDefinition wave)
        {
            for (int i = 0; i < wave.Count; i++)
            {
                EnemyType  type   = wave.EnemyTypes[i % wave.EnemyTypes.Length];
                GameObject prefab = PrefabForType(type);

                if (prefab == null)
                {
                    Debug.LogWarning($"[WaveDirector] No prefab wired for {type} — skipping enemy.");
                    continue;
                }

                Vector3 pos = i < wave.SpawnPositions.Length
                    ? wave.SpawnPositions[i]
                    : new Vector3(Random.Range(-22f, 22f), 0f, Random.Range(-22f, 22f));

                pos.y = type == EnemyType.SphereAerial ? 6f : 0f;
                Instantiate(prefab, pos, Quaternion.identity);
            }

            _totalSpawned++;
            Debug.Log($"[WaveDirector] Wave {_totalSpawned}: {wave.Count} enemies at t={SongTime():F1}s");
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
