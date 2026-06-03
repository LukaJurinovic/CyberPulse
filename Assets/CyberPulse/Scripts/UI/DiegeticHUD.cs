using UnityEngine;
using CyberPulse.Player;
using CyberPulse.Systems;
using CyberPulse.Weapons;
using CyberPulse.World;

namespace CyberPulse.UI
{
    public class DiegeticHUD : MonoBehaviour
    {
        [SerializeField] private PlayerStats  _playerStats;
        [SerializeField] private WeaponHolder _weaponHolder;

        private Texture2D _pixel;
        private float     _damageFlash;
        private float     _scorePop;
        private int       _lastScore;

        private GUIStyle _smLabel;
        private GUIStyle _mdLabel;
        private GUIStyle _traceLabel;
        private GUIStyle _syncLabel;
        private GUIStyle _weaponLabel;
        private GUIStyle _ammoLabel;
        private GUIStyle _scoreLabel;
        private GUIStyle _extractLabel;

        private static readonly Color CColour   = new Color(0f,    0.96f, 1f,   1f);
        private static readonly Color WColour   = new Color(1f,    0.85f, 0.1f, 1f);
        private static readonly Color DColour   = new Color(1f,    0.25f, 0.1f, 1f);
        private static readonly Color TraceNorm = new Color(0f,    0.9f,  1f,   1f);
        private static readonly Color TraceWarn = new Color(1f,    0.85f, 0.1f, 1f);
        private static readonly Color TraceCrit = new Color(1f,    0.3f,  0.1f, 1f);
        private static readonly Color SyncColor = new Color(1f,    0.2f,  0.8f, 1f);
        private static readonly Color BarBG     = new Color(0.08f, 0.10f, 0.15f, 0.85f);

        private void Awake()
        {
            _pixel = new Texture2D(1, 1);
            _pixel.SetPixel(0, 0, Color.white);
            _pixel.Apply();
        }

        private void Start()
        {
            if (_playerStats != null)
                _playerStats.OnDamageTaken += OnDamageTaken;
        }

        private void OnDestroy()
        {
            if (_playerStats != null)
                _playerStats.OnDamageTaken -= OnDamageTaken;
        }

        private void OnDamageTaken(int _) => _damageFlash = 1f;

        private void LateUpdate()
        {
            if (_damageFlash > 0f)
                _damageFlash = Mathf.Max(0f, _damageFlash - Time.deltaTime * 5f);

            var sm = ScoreManager.Instance;
            if (sm != null && sm.Score != _lastScore) { _scorePop = 1f; _lastScore = sm.Score; }
            if (_scorePop > 0f)
                _scorePop = Mathf.Max(0f, _scorePop - Time.deltaTime * 4f);
        }

        private void OnGUI()
        {
            if (_pixel == null) return;
            EnsureStyles();

            float sw = Screen.width;
            float sh = Screen.height;

            DrawDamageFlash(sw, sh);
            DrawTopLeft();
            DrawTopCenter(sw);
            DrawBottomLeft(sh);
            DrawBottomRight(sw, sh);
            DrawExtractPrompt(sw, sh);
        }

        private void DrawDamageFlash(float sw, float sh)
        {
            if (_damageFlash <= 0f) return;
            GUI.color = new Color(0.9f, 0.05f, 0.05f, _damageFlash * 0.55f);
            GUI.DrawTexture(new Rect(0, 0, sw, sh), _pixel);
            GUI.color = Color.white;
        }

        private void DrawTopLeft()
        {
            var gm = GameManager.Instance;
            if (gm != null)
            {
                GUI.color = CColour;
                GUI.Label(new Rect(20, 18, 240, 22), $"PHASE  {gm.CurrentPhase}", _smLabel);
                GUI.color = Color.white;
            }

            var wd = WaveDirector.Instance;
            if (wd != null && wd.WaveCount > 0)
            {
                int wave = Mathf.Min(wd.WaveIndex, wd.WaveCount);
                GUI.color = CColour;
                GUI.Label(new Rect(20, 40, 240, 22), $"WAVE  {wave} / {wd.WaveCount}", _smLabel);
                GUI.color = Color.white;
            }
        }

        private void DrawTopCenter(float sw)
        {
            var lm = LayerManager.Instance;
            if (lm != null && lm.ActiveLayer != null)
            {
                var layer = lm.ActiveLayer;

                GUI.color = CColour;
                GUI.Label(new Rect(sw * 0.5f - 120f, 14f, 240f, 22f),
                    $"LAYER  {lm.ActiveIndex + 1} / {lm.LayerCount}", _mdLabel);

                Color nodeColour = layer.NodesTotal > 0 && layer.NodesSiphoned >= layer.NodesTotal
                    ? new Color(0f, 1f, 0.4f, 1f)
                    : CColour;
                _mdLabel.normal.textColor = nodeColour;
                GUI.Label(new Rect(sw * 0.5f - 120f, 36f, 240f, 22f),
                    $"NODES  {layer.NodesSiphoned} / {layer.NodesTotal}      ENEMIES  {layer.EnemiesAlive}",
                    _mdLabel);
                _mdLabel.normal.textColor = CColour;

                if (layer.Cleared && !layer.IsTopLayer)
                {
                    float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 4f);
                    GUI.color = new Color(0f, 1f, 0.35f, pulse);
                    GUI.Label(new Rect(sw * 0.5f - 150f, 58f, 300f, 22f),
                        "▲  LAYER CLEAR — ASCEND  ▲", _mdLabel);
                }
                GUI.color = Color.white;
                return;
            }

            var mgr = DataNodeManager.Instance;
            if (mgr == null || mgr.TotalCount == 0) return;
            GUI.color = CColour;
            GUI.Label(new Rect(sw * 0.5f - 90f, 18f, 180f, 24f),
                $"NODES  {mgr.SiphonedCount} / {mgr.TotalCount}", _mdLabel);
            GUI.color = Color.white;
        }

        private void DrawBottomLeft(float sh)
        {
            const float pad  = 20f;
            const float barW = 200f;
            const float barH = 8f;

            float tn = TraceMeter.Instance != null ? TraceMeter.Instance.Normalized : 0f;
            float tv = TraceMeter.Instance != null ? TraceMeter.Instance.Value      : 0f;
            Color tc = tn < 0.5f ? TraceNorm : tn < 0.8f ? TraceWarn : TraceCrit;

            _traceLabel.normal.textColor = tc;
            GUI.Label(new Rect(pad, sh - 88f, 200f, 22f), $"TRACE  {Mathf.RoundToInt(tv)}%", _traceLabel);

            float barY = sh - 68f;
            GUI.color = BarBG;
            GUI.DrawTexture(new Rect(pad, barY, barW, barH), _pixel);
            GUI.color = tc;
            GUI.DrawTexture(new Rect(pad, barY, barW * tn, barH), _pixel);
            GUI.color = Color.white;

            float sn = SyncGauge.Instance != null ? SyncGauge.Instance.Normalized : 0f;
            Color sc = SyncColor;
            if (sn >= 1f)
            {
                float pulse = Mathf.Abs(Mathf.Sin(Time.unscaledTime * Mathf.PI * 4f));
                float b     = Mathf.Lerp(0.8f, 1f, pulse);
                sc = new Color(SyncColor.r * b, SyncColor.g * b, SyncColor.b * b, 1f);
            }

            _syncLabel.normal.textColor = sc;
            GUI.Label(new Rect(pad, sh - 52f, 80f, 18f), "SYNC", _syncLabel);

            const float syncW = 140f;
            float syncBarY = sh - 36f;
            GUI.color = BarBG;
            GUI.DrawTexture(new Rect(pad, syncBarY, syncW, 6f), _pixel);
            GUI.color = sc;
            GUI.DrawTexture(new Rect(pad, syncBarY, syncW * sn, 6f), _pixel);
            GUI.color = Color.white;
        }

        private void DrawBottomRight(float sw, float sh)
        {
            const float w = 220f;
            float x = sw - w - 20f;

            if (_weaponHolder != null && _weaponHolder.ActiveWeapon != null)
            {
                var weapon = _weaponHolder.ActiveWeapon;
                GUI.color = CColour;
                GUI.Label(new Rect(x, sh - 90f, w, 22f), weapon.WeaponName, _weaponLabel);

                Color ac = weapon.CurrentAmmo > 0 ? WColour : DColour;
                _ammoLabel.normal.textColor = ac;
                GUI.Label(new Rect(x, sh - 66f, w, 28f),
                    $"{weapon.CurrentAmmo}  /  {weapon.ReserveAmmo}", _ammoLabel);
            }

            if (ScoreManager.Instance != null)
            {
                int   combo = ScoreManager.Instance.ComboCount;
                Color sc2   = _scorePop > 0f
                    ? Color.Lerp(CColour, Color.white, _scorePop)
                    : combo > 1 ? WColour : CColour;
                _scoreLabel.normal.textColor = sc2;
                string text = combo > 1
                    ? $"{ScoreManager.Instance.Score:D6}  x{combo}"
                    : ScoreManager.Instance.Score.ToString("D6");
                GUI.Label(new Rect(x, sh - 38f, w, 22f), text, _scoreLabel);
            }
        }

        private void DrawExtractPrompt(float sw, float sh)
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.CurrentPhase != GamePhase.Extract) return;
            float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 4f);
            GUI.color = new Color(0f, 1f, 0.35f, pulse);
            GUI.Label(new Rect(sw * 0.5f - 150f, sh * 0.5f - 60f, 300f, 40f),
                "▶  REACH THE EXIT  ◀", _extractLabel);
            GUI.color = Color.white;
        }

        private void EnsureStyles()
        {
            if (_smLabel != null) return;

            _smLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 12,
                fontStyle = FontStyle.Bold,
                normal    = { textColor = CColour }
            };
            _mdLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = CColour }
            };
            _traceLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 13,
                fontStyle = FontStyle.Bold,
                normal    = { textColor = CColour }
            };
            _syncLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 11,
                fontStyle = FontStyle.Bold,
                normal    = { textColor = SyncColor }
            };
            _weaponLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperRight,
                normal    = { textColor = CColour }
            };
            _ammoLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperRight,
                normal    = { textColor = WColour }
            };
            _scoreLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperRight,
                normal    = { textColor = CColour }
            };
            _extractLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = Color.white }
            };
        }
    }
}
