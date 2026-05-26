using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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

        private bool _busy;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Start()
        {
            if (_fadePanel != null)
                StartCoroutine(FadePanel(1f, 0f, _fadeInTime));

            if (_titleText != null)
                StartCoroutine(TitleFlicker());
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
