using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using CyberPulse.Systems;

namespace CyberPulse.UI
{
    /// <summary>
    /// Drives the main menu — fades in on load, fades out before scene transition.
    /// Called by button OnClick events wired in MainMenuBuilder.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        private const string GameScene = "PlayableTestLevel";

        [SerializeField] private Image  _fadePanel;
        [SerializeField] private float  _fadeInTime  = 0.8f;
        [SerializeField] private float  _fadeOutTime = 0.5f;

        [Header("Title flicker")]
        [SerializeField] private TMPro.TextMeshProUGUI _titleText;
        [SerializeField] private float _flickerInterval = 4f;

        [Header("Song selection")]
        [SerializeField] private AudioClip[] _songs;
        [SerializeField] private AudioSource _menuMusicSource;

        private bool _busy;

        private int    _selectedBundled = 0;

        private string _selectedUserPath;

        private string[]  _userTrackPaths = new string[0];
        private Vector2   _scrollPos;
        private Coroutine _trackLoadRoutine;

        private GUIStyle _songHeader, _songSection, _songRow, _songRowSel, _importBtn;
        private bool     _stylesBuilt;

        private static readonly string[] AudioExtensions = { ".mp3", ".wav", ".ogg" };

        private void Start()
        {
            if (_fadePanel != null)
                StartCoroutine(FadePanel(1f, 0f, _fadeInTime));

            if (_titleText != null)
                StartCoroutine(TitleFlicker());

            if (_menuMusicSource == null)
                _menuMusicSource = GetComponent<AudioSource>();

            ScanUserMusicFolder();

            if (_songs != null && _songs.Length > 0)
            {
                _selectedBundled  = 0;
                SongSelection.Clip     = _songs[0];
                SongSelection.SongName = _songs[0] != null ? _songs[0].name : null;
                SongSelection.FilePath = null;
                if (_menuMusicSource != null && _songs[0] != null && !_menuMusicSource.isPlaying)
                    PlayMenuTrack(_songs[0]);
            }
        }

        public void OnStartGame()
        {
            if (_busy) return;
            _busy = true;
            CommitSelection();
            StartCoroutine(LoadGame());
        }

        public void OnQuit()
        {
            if (_busy) return;
            _busy = true;
            StartCoroutine(QuitSequence());
        }

        private void OnGUI()
        {
            if (_busy) return;
            EnsureSongStyles();

            float sw = Screen.width, sh = Screen.height;
            float panelW   = Mathf.Min(440f, sw * 0.30f);
            float x        = sw * 0.06f;
            float panelTop = sh * 0.38f;

            GUI.Label(new Rect(x, panelTop, panelW, 26f), "SELECT TRACK", _songHeader);

            float listH    = sh * 0.38f;
            var   listRect = new Rect(x, panelTop + 36f, panelW, listH);

            int totalRows  = BundledCount() + _userTrackPaths.Length;
            int sectionHeaders = 1 + (_userTrackPaths.Length > 0 ? 1 : 0);
            float innerH   = Mathf.Max(listH, (totalRows + sectionHeaders) * 38f + 16f);

            _scrollPos = GUI.BeginScrollView(listRect, _scrollPos, new Rect(0, 0, panelW - 16f, innerH));
            float iy = 8f;

            GUI.Label(new Rect(0, iy, panelW - 16f, 22f), "BUNDLED", _songSection);
            iy += 28f;

            if (_songs != null)
            {
                for (int i = 0; i < _songs.Length; i++)
                {
                    if (_songs[i] == null) continue;
                    bool sel  = _selectedUserPath == null && _selectedBundled == i;
                    string label = (sel ? ">  " : "    ") + _songs[i].name;
                    if (GUI.Button(new Rect(0, iy, panelW - 16f, 34f), label, sel ? _songRowSel : _songRow))
                    {
                        _selectedBundled  = i;
                        _selectedUserPath = null;
                        PlayMenuTrack(_songs[i]);
                    }
                    iy += 38f;
                }
            }

            if (_userTrackPaths.Length > 0)
            {
                iy += 8f;
                GUI.Label(new Rect(0, iy, panelW - 16f, 22f), "IMPORTED", _songSection);
                iy += 28f;

                foreach (string path in _userTrackPaths)
                {
                    bool sel   = path == _selectedUserPath;
                    string name = Path.GetFileNameWithoutExtension(path);
                    string label = (sel ? ">  " : "    ") + name;
                    if (GUI.Button(new Rect(0, iy, panelW - 16f, 34f), label, sel ? _songRowSel : _songRow))
                    {
                        _selectedUserPath = path;
                        if (_trackLoadRoutine != null) StopCoroutine(_trackLoadRoutine);
                        _trackLoadRoutine = StartCoroutine(LoadAndPlayUserTrack(path));
                    }
                    iy += 38f;
                }
            }

            GUI.EndScrollView();

            float btnY = panelTop + 36f + listH + 8f;

            if (GUI.Button(new Rect(x, btnY, panelW, 34f), "[ IMPORT TRACK ]", _importBtn))
                ImportTrack();

            if (GUI.Button(new Rect(x, btnY + 42f, panelW, 34f), "[ OPEN MUSIC FOLDER ]", _importBtn))
                OpenMusicFolder();
        }

        private void ScanUserMusicFolder()
        {
            string folder = UserMusicFolder();
            if (!Directory.Exists(folder))
            {
                _userTrackPaths = new string[0];
                return;
            }

            var found = new System.Collections.Generic.List<string>();
            foreach (string ext in AudioExtensions)
                found.AddRange(Directory.GetFiles(folder, "*" + ext));
            found.Sort();
            _userTrackPaths = found.ToArray();
        }

        private void ImportTrack()
        {
            string src = WindowsFilePicker.OpenAudioFile();
            if (string.IsNullOrEmpty(src) || !File.Exists(src)) return;

            string folder = UserMusicFolder();
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            string dest = Path.Combine(folder, Path.GetFileName(src));
            if (!File.Exists(dest))
                File.Copy(src, dest);

            ScanUserMusicFolder();

            _selectedUserPath = dest;
        }

        private void OpenMusicFolder()
        {
            string folder = UserMusicFolder();
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            System.Diagnostics.Process.Start("explorer.exe", folder.Replace('/', '\\'));
#endif
        }

        private void PlayMenuTrack(AudioClip clip)
        {
            if (_menuMusicSource == null || clip == null) return;
            if (_menuMusicSource.clip == clip && _menuMusicSource.isPlaying) return;
            _menuMusicSource.clip = clip;
            _menuMusicSource.Play();
        }

        private IEnumerator LoadAndPlayUserTrack(string path)
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

            if (req.result == UnityWebRequest.Result.Success && _selectedUserPath == path)
                PlayMenuTrack(DownloadHandlerAudioClip.GetContent(req));
            else if (req.result != UnityWebRequest.Result.Success)
                Debug.LogWarning($"[MainMenu] Failed to load preview '{path}': {req.error}");

            _trackLoadRoutine = null;
        }

        private void CommitSelection()
        {
            if (_selectedUserPath != null && File.Exists(_selectedUserPath))
            {
                SongSelection.FilePath = _selectedUserPath;
                SongSelection.Clip     = null;
                SongSelection.SongName = null;
            }
            else if (_songs != null && _selectedBundled >= 0 && _selectedBundled < _songs.Length
                     && _songs[_selectedBundled] != null)
            {
                SongSelection.Clip     = _songs[_selectedBundled];
                SongSelection.SongName = _songs[_selectedBundled].name;
                SongSelection.FilePath = null;
            }
        }

        private static string UserMusicFolder() =>
            Path.Combine(Application.persistentDataPath, "Music");

        private int BundledCount()
        {
            if (_songs == null) return 0;
            int n = 0;
            foreach (var c in _songs) if (c != null) n++;
            return n;
        }

        private IEnumerator LoadGame()
        {
            if (_fadePanel != null)
                yield return StartCoroutine(FadePanel(0f, 1f, _fadeOutTime));
            SceneManager.LoadScene(GameScene);
        }

        private IEnumerator QuitSequence()
        {
            if (_fadePanel != null)
                yield return StartCoroutine(FadePanel(0f, 1f, _fadeOutTime));
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        private IEnumerator FadePanel(float from, float to, float duration)
        {
            if (_fadePanel == null) yield break;
            float elapsed = 0f;
            var color = _fadePanel.color;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                color.a = Mathf.Lerp(from, to, elapsed / duration);
                _fadePanel.color = color;
                yield return null;
            }
            color.a = to;
            _fadePanel.color = color;
        }

        private IEnumerator TitleFlicker()
        {
            const string Chars    = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%";
            const string Original = "CYBER-PULSE";
            while (true)
            {
                yield return new WaitForSeconds(_flickerInterval + Random.Range(-1f, 1f));

                for (int f = 0; f < 3; f++)
                {
                    var glitched = new System.Text.StringBuilder(Original);
                    int pos = Random.Range(0, Original.Length);
                    glitched[pos] = Chars[Random.Range(0, Chars.Length)];
                    _titleText.text = glitched.ToString();
                    yield return new WaitForSecondsRealtime(0.06f);
                    _titleText.text = Original;
                    yield return new WaitForSecondsRealtime(0.05f);
                }
            }
        }

        private void EnsureSongStyles()
        {
            if (_stylesBuilt) return;

            _songHeader = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft,
            };
            _songHeader.normal.textColor = new Color(0f, 0.96f, 1f, 0.85f);

            _songSection = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft,
            };
            _songSection.normal.textColor = new Color(0.4f, 0.5f, 0.6f, 1f);

            _songRow = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14, alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(10, 10, 6, 6),
            };
            _songRow.normal.textColor = new Color(0.5f, 0.6f, 0.7f, 1f);

            _songRowSel = new GUIStyle(_songRow);
            _songRowSel.normal.textColor = new Color(0f, 0.96f, 1f, 1f);
            _songRowSel.fontStyle        = FontStyle.Bold;

            _importBtn = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
            };
            _importBtn.normal.textColor = new Color(0.4f, 0.85f, 1f, 1f);
            _importBtn.hover.textColor  = Color.white;

            _stylesBuilt = true;
        }
    }
}
