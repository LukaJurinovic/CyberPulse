using UnityEngine;
using CyberPulse.Player;
using CyberPulse.Systems;
using CyberPulse.World;

namespace CyberPulse.UI
{
    /// <summary>
    /// Immediate-mode HUD for playtesting. No Canvas required.
    /// Shows: HP bar, TraceMeter bar, node progress, current phase, damage flash.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        [SerializeField] private PlayerStats _playerStats;

        private float    _damageFlashAlpha;
        private Texture2D _pixel;

        private GUIStyle _barLabelStyle;
        private GUIStyle _infoStyle;
        private GUIStyle _phaseStyle;
        private GUIStyle _extractStyle;

        private static readonly Color ColHealth  = new Color(0.15f, 1f,    0.45f);
        private static readonly Color ColTrace   = new Color(1f,    0.35f, 0.1f);
        private static readonly Color ColAlert   = new Color(1f,    0.55f, 0f);
        private static readonly Color ColCrit    = new Color(1f,    0.1f,  0.1f);
        private static readonly Color ColCyan    = new Color(0f,    0.96f, 1f);
        private static readonly Color ColBg      = new Color(0.05f, 0.05f, 0.08f, 0.75f);

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            _pixel = new Texture2D(1, 1);
            _pixel.SetPixel(0, 0, Color.white);
            _pixel.Apply();
        }

        private void OnEnable()
        {
            if (_playerStats != null)
                _playerStats.OnDamageTaken += OnDamageTaken;
        }

        private void OnDisable()
        {
            if (_playerStats != null)
                _playerStats.OnDamageTaken -= OnDamageTaken;
        }

        private void Update()
        {
            if (_damageFlashAlpha > 0f)
                _damageFlashAlpha = Mathf.Max(0f, _damageFlashAlpha - Time.deltaTime * 5f);
        }

        private void OnDamageTaken(int amount)
        {
            _damageFlashAlpha = Mathf.Min(1f, _damageFlashAlpha + 0.45f);
        }

        // ── Rendering ─────────────────────────────────────────────────────────

        private void OnGUI()
        {
            if (_pixel == null) return;
            EnsureStyles();

            float sw = Screen.width;
            float sh = Screen.height;

            DrawDamageFlash(sw, sh);
            DrawHealthBar(sw, sh);
            DrawTraceMeterBar(sw, sh);
            DrawNodeProgress(sw);
            DrawPhase();
            DrawExtractPrompt(sw, sh);
        }

        private void DrawDamageFlash(float sw, float sh)
        {
            if (_damageFlashAlpha <= 0f) return;
            GUI.color = new Color(0.9f, 0.05f, 0.05f, _damageFlashAlpha * 0.55f);
            GUI.DrawTexture(new Rect(0, 0, sw, sh), _pixel);
            GUI.color = Color.white;
        }

        private void DrawHealthBar(float sw, float sh)
        {
            if (_playerStats == null) return;
            float norm = Mathf.Clamp01((float)_playerStats.CurrentHealth / _playerStats.MaxHealth);
            Color col = norm < 0.3f ? ColCrit : ColHealth;
            DrawBar(20, sh - 44, 200, 20, norm, col, $"HP  {_playerStats.CurrentHealth}/{_playerStats.MaxHealth}");
        }

        private void DrawTraceMeterBar(float sw, float sh)
        {
            var trace = TraceMeter.Instance;
            if (trace == null) return;

            float norm = trace.Normalized;
            Color col  = norm >= 0.8f ? ColCrit : norm >= 0.5f ? ColAlert : ColTrace;
            DrawBar(sw - 220, sh - 44, 200, 20, norm, col, $"TRACE  {Mathf.RoundToInt(trace.Value)}%");
        }

        private void DrawNodeProgress(float sw)
        {
            var mgr = DataNodeManager.Instance;
            if (mgr == null || mgr.TotalCount == 0) return;

            string text = $"NODES  {mgr.SiphonedCount} / {mgr.TotalCount}";
            float w = 160f;
            GUI.color = ColCyan;
            GUI.Label(new Rect(sw * 0.5f - w * 0.5f, 18, w, 24), text, _infoStyle);
            GUI.color = Color.white;
        }

        private void DrawPhase()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            GUI.color = ColCyan;
            GUI.Label(new Rect(20, 18, 220, 22), $"PHASE  {gm.CurrentPhase}", _phaseStyle);
            GUI.color = Color.white;
        }

        private void DrawExtractPrompt(float sw, float sh)
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.CurrentPhase != GamePhase.Extract) return;

            // Pulse between full and half alpha using unscaled time.
            float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 4f);
            GUI.color = new Color(0f, 1f, 0.35f, pulse);
            GUI.Label(new Rect(sw * 0.5f - 150f, sh * 0.5f - 60f, 300f, 40f), "▶  REACH THE EXIT  ◀", _extractStyle);
            GUI.color = Color.white;
        }

        // ── Bar helper ────────────────────────────────────────────────────────

        private void DrawBar(float x, float y, float w, float h, float fill, Color fillColor, string label)
        {
            // Background
            GUI.color = ColBg;
            GUI.DrawTexture(new Rect(x, y, w, h), _pixel);

            // Fill
            GUI.color = fillColor;
            float fillW = Mathf.Max(0f, (w - 2f) * fill);
            GUI.DrawTexture(new Rect(x + 1, y + 1, fillW, h - 2f), _pixel);

            // Label
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 5, y + 1, w - 5, h), label, _barLabelStyle);
        }

        // ── Style init (done in OnGUI to avoid pre-init issues) ───────────────

        private void EnsureStyles()
        {
            if (_barLabelStyle != null) return;

            _barLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 11,
                fontStyle = FontStyle.Bold,
                normal    = { textColor = Color.white }
            };
            _infoStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = ColCyan }
            };
            _phaseStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 12,
                fontStyle = FontStyle.Bold,
                normal    = { textColor = ColCyan }
            };
            _extractStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = Color.white }
            };
        }
    }
}
