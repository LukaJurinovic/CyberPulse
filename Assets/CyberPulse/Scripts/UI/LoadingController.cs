using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using CyberPulse.Systems;

namespace CyberPulse.UI
{
    /// <summary>
    /// Boot orchestrator for the gameplay scene. Holds music playback until the
    /// chosen song has been analysed offline, shows an OnGUI loading screen with the
    /// detected BPM/duration/intensity, then starts playback — which begins the song
    /// clock that BeatClock and WaveDirector run on. Analysing before the first note
    /// means wave timing and the beat window are aligned from bar one.
    ///
    /// Wired by PlayableLevelBuilder, which also sets the music sources' playOnAwake,
    /// SongAnalyzer._analyzeOnStart and DynamicMusicPlayer._playOnStart to false so
    /// this controller owns the boot sequence. If those gates are left at their
    /// defaults (e.g. an old scene with no LoadingController) the systems self-start
    /// exactly as before — this component is purely additive.
    /// </summary>
    public class LoadingController : MonoBehaviour
    {
        [Header("Audio")]
        [SerializeField] private AudioSource        _ambientSrc;
        [SerializeField] private AudioSource        _actionSrc;
        [SerializeField] private SongAnalyzer       _analyzer;
        [SerializeField] private DynamicMusicPlayer _musicPlayer;

        [Header("Bundled clips (name-matched to the menu's SongSelection)")]
        [SerializeField] private AudioClip[] _songs;

        [Header("Timing (unscaled seconds)")]
        [SerializeField] private float _minDisplaySeconds = 1.6f;  // floor so a fast analysis doesn't flash by
        [SerializeField] private float _resultHoldSeconds = 1.4f;  // dwell on the BPM readout before starting
        [SerializeField] private float _fadeSeconds       = 0.5f;

        private enum Stage { Analyzing, Result, Done }
        private Stage       _stage = Stage.Analyzing;
        private float       _alpha = 1f;
        private SongProfile _profile;
        private bool        _haveProfile;
        private string      _songName = "SIGNAL";

        private Texture2D _pixel;
        private GUIStyle  _head, _sub, _data, _hint;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            _pixel = new Texture2D(1, 1);
            _pixel.SetPixel(0, 0, Color.white);
            _pixel.Apply();
        }

        private void Start() => StartCoroutine(Boot());

        private IEnumerator Boot()
        {
            // Freeze gameplay while we analyse. Frame-driven coroutines (the analysis
            // routine and this one) still advance at timeScale 0 because they yield on
            // frames, not on scaled time; the overlay uses unscaled time throughout.
            Time.timeScale = 0f;

            // ── Resolve the chosen clip and load it onto the music sources ──────
            // FilePath (user-imported) is checked first so it always wins over the bundled fallback.
            AudioClip clip = null;
            if (!string.IsNullOrEmpty(SongSelection.FilePath))
                yield return LoadClipFromFile(SongSelection.FilePath, loaded => clip = loaded);
            if (clip == null)
                clip = ResolveSelectedClip();

            if (clip != null)
            {
                _songName = clip.name;
                if (_ambientSrc != null) _ambientSrc.clip = clip;
                if (_actionSrc  != null) _actionSrc.clip  = clip;   // single-stem: both sources share the song
            }
            else if (_ambientSrc != null && _ambientSrc.clip != null)
            {
                clip      = _ambientSrc.clip;
                _songName = clip.name;
            }

            float startTime = Time.unscaledTime;

            // ── Kick off offline analysis and wait for the profile ─────────────
            if (_analyzer != null)
            {
                _analyzer.OnAnalysisComplete += OnAnalysisComplete;
                _analyzer.AnalyzeClip(clip);
                while (!_haveProfile) yield return null;
                _analyzer.OnAnalysisComplete -= OnAnalysisComplete;
            }

            _stage = Stage.Result;

            // Hold the readout: at least _minDisplaySeconds total, then dwell _resultHoldSeconds.
            float elapsed = Time.unscaledTime - startTime;
            if (elapsed < _minDisplaySeconds)
                yield return WaitUnscaled(_minDisplaySeconds - elapsed);
            yield return WaitUnscaled(_resultHoldSeconds);

            // ── Start the song + unfreeze gameplay ─────────────────────────────
            Time.timeScale = 1f;
            if (_musicPlayer != null)      _musicPlayer.BeginPlayback();
            else if (_ambientSrc != null)  _ambientSrc.Play();

            // ── Fade the overlay out (unscaled) ────────────────────────────────
            _stage = Stage.Done;
            float t = 0f;
            while (t < _fadeSeconds)
            {
                t     += Time.unscaledDeltaTime;
                _alpha = 1f - Mathf.Clamp01(t / _fadeSeconds);
                yield return null;
            }
            _alpha  = 0f;
            enabled = false;   // stop OnGUI once fully transparent
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private AudioClip ResolveSelectedClip()
        {
            string want = SongSelection.SongName;
            if (_songs != null)
            {
                if (!string.IsNullOrEmpty(want))
                    foreach (var c in _songs)
                        if (c != null && c.name == want) return c;

                // Default: first available bundled clip.
                foreach (var c in _songs)
                    if (c != null) return c;
            }
            return null;
        }

        private void OnAnalysisComplete(SongProfile profile)
        {
            _profile     = profile;
            _haveProfile = true;
        }

        private static IEnumerator LoadClipFromFile(string path, System.Action<AudioClip> onDone)
        {
            string uri = "file:///" + path.Replace('\\', '/');
            AudioType type = Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".wav" => AudioType.WAV,
                ".ogg" => AudioType.OGGVORBIS,
                _      => AudioType.MPEG,
            };

            using var req = UnityWebRequestMultimedia.GetAudioClip(uri, type);
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                var clip = DownloadHandlerAudioClip.GetContent(req);
                clip.name = Path.GetFileNameWithoutExtension(path);
                onDone(clip);
            }
            else
            {
                Debug.LogWarning($"[LoadingController] Failed to load '{path}': {req.error}");
                onDone(null);
            }
        }

        private static IEnumerator WaitUnscaled(float seconds)
        {
            float t = 0f;
            while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
        }

        // ── Overlay ──────────────────────────────────────────────────────────

        private void OnGUI()
        {
            if (_alpha <= 0f) return;
            EnsureStyles();

            float sw = Screen.width, sh = Screen.height;

            GUI.color = new Color(0.01f, 0.02f, 0.05f, 0.96f * _alpha);
            GUI.DrawTexture(new Rect(0, 0, sw, sh), _pixel);
            GUI.color = Color.white;

            float cy = sh * 0.34f;
            float x  = sw * 0.5f - 360f;

            _head.normal.textColor = new Color(0f, 0.96f, 1f, _alpha);
            GUI.Label(new Rect(x, cy, 720f, 70f), "DECODING SIGNAL", _head);

            _sub.normal.textColor = new Color(0.5f, 0.62f, 0.72f, _alpha);
            GUI.Label(new Rect(x, cy + 64f, 720f, 32f), _songName.ToUpperInvariant(), _sub);

            if (_stage == Stage.Analyzing || !_haveProfile)
            {
                int dots = (int)(Time.unscaledTime * 3f) % 4;
                _hint.normal.textColor = new Color(0.4f, 0.85f, 1f, _alpha);
                GUI.Label(new Rect(x, cy + 120f, 720f, 30f),
                    "ANALYZING WAVEFORM" + new string('.', dots), _hint);
                return;
            }

            // Detected BPM — the headline readout.
            _data.normal.textColor = new Color(1f, 0.2f, 0.8f, _alpha);
            GUI.Label(new Rect(x, cy + 112f, 720f, 60f), $"{_profile.BPM:F0} BPM", _data);

            int min = (int)(_profile.Duration / 60f);
            int sec = (int)(_profile.Duration % 60f);
            _sub.normal.textColor = new Color(0.55f, 0.65f, 0.78f, _alpha);
            GUI.Label(new Rect(x, cy + 176f, 720f, 28f),
                $"DURATION {min}:{sec:D2}   ·   INTENSITY {Mathf.RoundToInt(_profile.AverageEnergy * 100f)}%", _sub);

            float pulse = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 4f);
            _hint.normal.textColor = new Color(0.4f, 0.85f, 1f, _alpha * pulse);
            GUI.Label(new Rect(x, cy + 220f, 720f, 26f), "SYNCING TO TRACK", _hint);
        }

        private void EnsureStyles()
        {
            if (_head != null) return;
            _head = new GUIStyle(GUI.skin.label) { fontSize = 46, fontStyle = FontStyle.Bold,   alignment = TextAnchor.MiddleCenter };
            _sub  = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Normal, alignment = TextAnchor.MiddleCenter };
            _data = new GUIStyle(GUI.skin.label) { fontSize = 40, fontStyle = FontStyle.Bold,   alignment = TextAnchor.MiddleCenter };
            _hint = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold,   alignment = TextAnchor.MiddleCenter };
        }
    }
}
