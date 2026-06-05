using UnityEngine;
using CyberPulse.Player;
using CyberPulse.Systems;
using CyberPulse.Weapons;
using CyberPulse.World;

namespace CyberPulse.UI
{
    public class GameHUD : MonoBehaviour
    {
        [SerializeField] private PlayerStats  _playerStats;
        [SerializeField] private WeaponHolder _weaponHolder;

        private float    _damageFlashAlpha;
        private Texture2D _pixel;

        private GUIStyle _barLabelStyle;
        private GUIStyle _infoStyle;
        private GUIStyle _phaseStyle;
        private GUIStyle _extractStyle;
        private GUIStyle _scoreStyle;
        private GUIStyle _comboStyle;

        private float _scorePop;
        private int   _lastScore;

        private static readonly Color ColHealth  = new Color(0.15f, 1f,    0.45f);
        private static readonly Color ColTrace   = new Color(1f,    0.35f, 0.1f);
        private static readonly Color ColAlert   = new Color(1f,    0.55f, 0f);
        private static readonly Color ColCrit    = new Color(1f,    0.1f,  0.1f);
        private static readonly Color ColCyan    = new Color(0f,    0.96f, 1f);
        private static readonly Color ColGold    = new Color(1f,    0.82f, 0.1f);
        private static readonly Color ColBg      = new Color(0.05f, 0.05f, 0.08f, 0.75f);

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

            var sm = ScoreManager.Instance;
            if (sm != null && sm.Score != _lastScore)
            {
                _scorePop  = 1f;
                _lastScore = sm.Score;
            }
            if (_scorePop > 0f)
                _scorePop = Mathf.Max(0f, _scorePop - Time.deltaTime * 4f);
        }

        private void OnDamageTaken(int amount)
        {
            _damageFlashAlpha = Mathf.Min(1f, _damageFlashAlpha + 0.45f);
        }

        private const float ReferenceHeight = 720f;

        private void OnGUI()
        {
            if (_pixel == null) return;
            EnsureStyles();

            float     scale      = Screen.height / ReferenceHeight;
            Matrix4x4 prevMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            float sw = Screen.width  / scale;
            float sh = Screen.height / scale;

            DrawDamageFlash(sw, sh);
            DrawHealthBar(sw, sh);
            DrawTraceMeterBar(sw, sh);
            DrawSyncBar(sw, sh);
            DrawAmmo(sw, sh);
            DrawNodeProgress(sw);
            DrawPhase(sw);
            DrawWaveInfo(sw);
            DrawScore(sw);
            DrawExtractPrompt(sw, sh);

            GUI.matrix = prevMatrix;
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

        private void DrawPhase(float sw)
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            GUI.color = ColCyan;
            GUI.Label(new Rect(20, 18, 220, 22), $"PHASE  {gm.CurrentPhase}", _phaseStyle);
            GUI.color = Color.white;
        }

        private void DrawSyncBar(float sw, float sh)
        {
            var sync = SyncGauge.Instance;
            if (sync == null) return;

            float w = 200f;
            float x = sw * 0.5f - w * 0.5f;
            float y = sh - 74f;
            DrawBar(x, y, w, 16, sync.Normalized, ColGold, $"SYNC  {Mathf.RoundToInt(sync.Value)}");
        }

        private void DrawAmmo(float sw, float sh)
        {
            if (_weaponHolder == null) return;
            var wep = _weaponHolder.ActiveWeapon;
            if (wep == null) return;

            string ammoText = $"{wep.CurrentAmmo} / {wep.ReserveAmmo}";
            GUI.color = wep.CurrentAmmo == 0 ? ColCrit : Color.white;
            GUI.Label(new Rect(sw - 140f, sh - 72f, 120f, 22f), ammoText, _phaseStyle);
            GUI.color = Color.white;
        }

        private void DrawWaveInfo(float sw)
        {
            var wd = WaveDirector.Instance;
            if (wd == null || wd.WaveCount == 0) return;

            int wave = Mathf.Min(wd.WaveIndex, wd.WaveCount);
            GUI.color = ColCyan;
            GUI.Label(new Rect(20, 40, 220, 22), $"WAVE  {wave} / {wd.WaveCount}", _phaseStyle);
            GUI.color = Color.white;
        }

        private void DrawScore(float sw)
        {
            var sm = ScoreManager.Instance;
            if (sm == null) return;

            EnsureStyles();

            float popScale = 1f + _scorePop * 0.4f;
            float w = 200f * popScale;
            float h = 28f  * popScale;
            Color scoreCol = Color.Lerp(ColCyan, Color.white, _scorePop);
            GUI.color = scoreCol;
            GUI.Label(new Rect(sw - w - 16f, 14f, w, h), $"{sm.Score:N0}", _scoreStyle);
            GUI.color = Color.white;

            if (sm.ComboCount > 1)
            {
                float comboAlpha = Mathf.Min(1f, sm.ComboTimer / 1f);
                GUI.color = new Color(1f, 0.85f, 0.15f, comboAlpha);
                GUI.Label(new Rect(sw - 200f, 44f, 200f, 24f), $"x{sm.ComboCount}  COMBO", _comboStyle);
                GUI.color = Color.white;
            }
        }

        private void DrawExtractPrompt(float sw, float sh)
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.CurrentPhase != GamePhase.Extract) return;

            float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 4f);
            GUI.color = new Color(0f, 1f, 0.35f, pulse);
            GUI.Label(new Rect(sw * 0.5f - 150f, sh * 0.5f - 60f, 300f, 40f), "▶  REACH THE EXIT  ◀", _extractStyle);
            GUI.color = Color.white;
        }

        private void DrawBar(float x, float y, float w, float h, float fill, Color fillColor, string label)
        {
            GUI.color = ColBg;
            GUI.DrawTexture(new Rect(x, y, w, h), _pixel);

            GUI.color = fillColor;
            float fillW = Mathf.Max(0f, (w - 2f) * fill);
            GUI.DrawTexture(new Rect(x + 1, y + 1, fillW, h - 2f), _pixel);

            GUI.color = Color.white;
            GUI.Label(new Rect(x + 5, y + 1, w - 5, h), label, _barLabelStyle);
        }

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
            _scoreStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight,
                normal    = { textColor = ColCyan }
            };
            _comboStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight,
                normal    = { textColor = new Color(1f, 0.85f, 0.15f) }
            };
        }
    }
}
