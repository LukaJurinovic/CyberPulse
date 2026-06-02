using System.Collections;
using UnityEngine;
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

        [SerializeField] private Image  _fadePanel;   // fullscreen black Image, alpha 0→1 on load
        [SerializeField] private float  _fadeInTime  = 0.8f;
        [SerializeField] private float  _fadeOutTime = 0.5f;

        [Header("Title flicker")]
        [SerializeField] private TMPro.TextMeshProUGUI _titleText;
        [SerializeField] private float _flickerInterval = 4f;   // seconds between glitch pops

        [Header("Song selection")]
        [SerializeField] private AudioClip[] _songs;            // bundled tracks, wired by MainMenuBuilder

        private bool _busy;

        private int      _selectedSong = 0;
        private GUIStyle _songHeader, _songRow, _songRowSel;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Start()
        {
            if (_fadePanel != null)
                StartCoroutine(FadePanel(1f, 0f, _fadeInTime));

            if (_titleText != null)
                StartCoroutine(TitleFlicker());

            // Default the selection to the first available track so launching the game
            // without touching the list still picks a valid song.
            if (_songs != null && _songs.Length > 0)
            {
                _selectedSong = Mathf.Clamp(_selectedSong, 0, _songs.Length - 1);
                SongSelection.SongName = _songs[_selectedSong] != null ? _songs[_selectedSong].name : null;
            }
        }

        // ── Public API (wired to button OnClick in builder) ───────────────────

        public void OnStartGame()
        {
            if (_busy) return;
            _busy = true;
            StartCoroutine(LoadGame());
        }

        public void OnQuit()
        {
            if (_busy) return;
            _busy = true;
            StartCoroutine(QuitSequence());
        }

        // ── Track picker (OnGUI, left panel) ──────────────────────────────────

        private void OnGUI()
        {
            if (_busy || _songs == null || _songs.Length == 0) return;
            EnsureSongStyles();

            float sw = Screen.width, sh = Screen.height;
            float panelW = Mathf.Min(440f, sw * 0.30f);
            float x = sw * 0.06f;
            float y = sh * 0.40f;

            GUI.Label(new Rect(x, y, panelW, 26f), "SELECT TRACK", _songHeader);
            y += 36f;

            for (int i = 0; i < _songs.Length; i++)
            {
                if (_songs[i] == null) continue;
                bool sel   = i == _selectedSong;
                string row = (sel ? ">  " : "    ") + _songs[i].name;
                if (GUI.Button(new Rect(x, y, panelW, 34f), row, sel ? _songRowSel : _songRow))
                {
                    _selectedSong          = i;
                    SongSelection.SongName = _songs[i].name;
                }
                y += 38f;
            }
        }

        private void EnsureSongStyles()
        {
            if (_songHeader != null) return;
            _songHeader = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft,
            };
            _songHeader.normal.textColor = new Color(0f, 0.96f, 1f, 0.85f);

            _songRow = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16, alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(10, 10, 6, 6),
            };
            _songRow.normal.textColor = new Color(0.5f, 0.6f, 0.7f, 1f);

            _songRowSel = new GUIStyle(_songRow);
            _songRowSel.normal.textColor = new Color(0f, 0.96f, 1f, 1f);
            _songRowSel.fontStyle        = FontStyle.Bold;
        }

        // ── Coroutines ────────────────────────────────────────────────────────

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

        // Brief random-character glitch on the title text every few seconds
        private IEnumerator TitleFlicker()
        {
            const string Chars    = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%";
            const string Original = "CYBER-PULSE";
            while (true)
            {
                yield return new WaitForSeconds(_flickerInterval + Random.Range(-1f, 1f));

                // Two quick flicker frames
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
    }
}
