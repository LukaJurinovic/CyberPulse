using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CyberPulse.Player;
using CyberPulse.Systems;
using CyberPulse.Weapons;

namespace CyberPulse.UI
{
    /// <summary>
    /// World-space HUD parented to the CameraPivot.
    ///
    /// Layout:
    ///   ┌──────────────────────────────────────┐
    ///   │  73%            ▐▐▐▐▐░░░░  TRACE    │
    ///   │  Assault Rifle     AMO 28/30  012500 │
    ///   │  SYNC ▐▐▐░░░░░░░░                   │
    ///   └──────────────────────────────────────┘
    ///
    /// HP is removed — Trace Meter is the sole health mechanic.
    /// </summary>
    public class DiegeticHUD : MonoBehaviour
    {
        [SerializeField] private PlayerStats  _playerStats;   // for damage flash only
        [SerializeField] private WeaponHolder _weaponHolder;
        [SerializeField] private Transform    _cameraPivot;

        [Header("Canvas positioning (relative to camera pivot)")]
        [SerializeField] private Vector3 _canvasOffset = new Vector3(-0.20f, -0.175f, 0.42f);
        [SerializeField] private float   _canvasWidth  = 0.38f;
        [SerializeField] private float   _canvasHeight = 0.11f;

        private TextMeshProUGUI _txtTrace;
        private TextMeshProUGUI _txtWeapon;
        private TextMeshProUGUI _txtAmmo;
        private TextMeshProUGUI _txtScore;
        private Image           _traceBar;
        private Image           _syncBar;

        private float _damageFlash;

        private static readonly Color CColour    = new Color(0f,    0.96f, 1f,    1f);
        private static readonly Color WColour    = new Color(1f,    0.85f, 0.1f,  1f);
        private static readonly Color DColour    = new Color(1f,    0.25f, 0.1f,  1f);
        private static readonly Color DimCol     = new Color(0.15f, 0.18f, 0.25f, 0.85f);
        private static readonly Color TraceNorm  = new Color(0f,    0.9f,  1f,    1f);
        private static readonly Color TraceCrit  = new Color(1f,    0.3f,  0.1f,  1f);
        private static readonly Color SyncColor  = new Color(1f,    0.2f,  0.8f,  1f);

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Start()
        {
            if (_cameraPivot == null) { enabled = false; return; }
            BuildCanvas();

            if (_playerStats != null)
                _playerStats.OnDamageTaken += _ => _damageFlash = 1f;
        }

        private void OnDestroy()
        {
            if (_playerStats != null)
                _playerStats.OnDamageTaken -= _ => _damageFlash = 1f;
        }

        private void LateUpdate()
        {
            RefreshStats();
        }

        // ── Canvas construction ───────────────────────────────────────────────

        private void BuildCanvas()
        {
            var canvasGO = new GameObject("DiegeticCanvas");
            canvasGO.transform.SetParent(_cameraPivot, false);
            canvasGO.transform.localPosition = _canvasOffset;
            canvasGO.transform.localRotation = Quaternion.identity;

            canvasGO.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;

            var rt = canvasGO.GetComponent<RectTransform>();
            const float PixPerMetre = 1000f;
            rt.sizeDelta  = new Vector2(_canvasWidth * PixPerMetre, _canvasHeight * PixPerMetre);
            rt.localScale = Vector3.one / PixPerMetre;

            var bg = MakeImage(rt, "BG", Vector2.zero, rt.sizeDelta, DimCol);
            var bgRT = bg.rectTransform;
            bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;

            float W = rt.sizeDelta.x;
            float H = rt.sizeDelta.y;
            float rowTop  = H * 0.72f;
            float rowBot  = H * 0.30f;
            float rowSync = H * 0.09f;

            // ── Top row: trace % + trace bar ──────────────────────────────────
            _txtTrace = MakeTMP(rt, "TraceVal", new Vector2(W * 0.13f, rowTop),
                new Vector2(W * 0.26f, H * 0.4f), "0%", 32, TraceNorm, TextAlignmentOptions.Center);

            var traceBase = MakeImage(rt, "TraceBG",
                new Vector2(W * 0.72f, rowTop), new Vector2(W * 0.44f, 18f),
                new Color(0.1f, 0.12f, 0.18f, 1f));
            _traceBar = MakeImage(rt, "TraceFill",
                Vector2.zero, new Vector2(W * 0.44f, 18f), TraceNorm);
            _traceBar.transform.SetParent(traceBase.transform, false);
            var trt = _traceBar.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            _traceBar.type = Image.Type.Filled;
            _traceBar.fillMethod = Image.FillMethod.Horizontal;

            MakeTMP(rt, "TraceLabel", new Vector2(W * 0.72f, rowTop + 18f),
                new Vector2(W * 0.44f, 18f), "TRACE", 18,
                new Color(0.6f, 0.7f, 0.8f, 1f), TextAlignmentOptions.Right);

            // ── Bottom row: weapon name | ammo | score ────────────────────────
            _txtWeapon = MakeTMP(rt, "WeaponName", new Vector2(W * 0.20f, rowBot),
                new Vector2(W * 0.36f, H * 0.4f), "—", 20, CColour, TextAlignmentOptions.Left);

            MakeTMP(rt, "AmmoLabel", new Vector2(W * 0.55f, rowBot),
                new Vector2(W * 0.10f, H * 0.4f), "AMO", 20,
                new Color(0.6f, 0.7f, 0.8f, 1f), TextAlignmentOptions.Left);
            _txtAmmo = MakeTMP(rt, "AmmoVal", new Vector2(W * 0.72f, rowBot),
                new Vector2(W * 0.24f, H * 0.4f), "30/30", 24, WColour, TextAlignmentOptions.Left);

            _txtScore = MakeTMP(rt, "Score", new Vector2(W * 0.86f, rowBot),
                new Vector2(W * 0.26f, H * 0.4f), "0", 20, CColour, TextAlignmentOptions.Right);

            // ── Sync row: SYNC label + bar ────────────────────────────────────
            MakeTMP(rt, "SyncLabel", new Vector2(W * 0.07f, rowSync),
                new Vector2(W * 0.10f, H * 0.25f), "SYNC", 16,
                new Color(0.8f, 0.5f, 0.9f, 1f), TextAlignmentOptions.Left);
            var syncBase = MakeImage(rt, "SyncBG",
                new Vector2(W * 0.30f, rowSync), new Vector2(W * 0.30f, 8f),
                new Color(0.12f, 0.03f, 0.14f, 1f));
            _syncBar = MakeImage(rt, "SyncFill", Vector2.zero, new Vector2(W * 0.30f, 8f), SyncColor);
            _syncBar.transform.SetParent(syncBase.transform, false);
            var srt = _syncBar.GetComponent<RectTransform>();
            srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one;
            srt.offsetMin = srt.offsetMax = Vector2.zero;
            _syncBar.type = Image.Type.Filled;
            _syncBar.fillMethod = Image.FillMethod.Horizontal;
        }

        // ── Runtime refresh ───────────────────────────────────────────────────

        private void RefreshStats()
        {
            // Weapon name + ammo
            if (_weaponHolder != null && _weaponHolder.ActiveWeapon != null)
            {
                var w = _weaponHolder.ActiveWeapon;
                _txtWeapon.text = w.WeaponName;
                _txtAmmo.text   = $"{w.CurrentAmmo}/{w.ReserveAmmo}";
                _txtAmmo.color  = w.CurrentAmmo > 0 ? WColour : DColour;
            }

            // Trace meter
            if (TraceMeter.Instance != null)
            {
                float tn = TraceMeter.Instance.Normalized;
                _traceBar.fillAmount = tn;
                Color tc             = tn < 0.5f ? TraceNorm : tn < 0.8f ? WColour : TraceCrit;
                _traceBar.color      = tc;
                _txtTrace.text       = $"{Mathf.RoundToInt(TraceMeter.Instance.Value)}%";
                _txtTrace.color      = tc;
            }

            // SYNC — pulse color when full
            if (_syncBar != null && SyncGauge.Instance != null)
            {
                float syncNorm = SyncGauge.Instance.Normalized;
                _syncBar.fillAmount = syncNorm;
                if (syncNorm >= 1f)
                {
                    float pulse = Mathf.Abs(Mathf.Sin(Time.unscaledTime * Mathf.PI * 4f));
                    float bright = Mathf.Lerp(1f, 1.5f, pulse);
                    _syncBar.color = new Color(
                        SyncColor.r * bright,
                        SyncColor.g * bright,
                        SyncColor.b * bright, 1f);
                }
                else
                {
                    _syncBar.color = SyncColor;
                }
            }

            // Score + combo
            if (ScoreManager.Instance != null)
            {
                int combo = ScoreManager.Instance.ComboCount;
                _txtScore.text  = combo > 1
                    ? $"{ScoreManager.Instance.Score:D6}  x{combo}"
                    : ScoreManager.Instance.Score.ToString("D6");
                _txtScore.color = combo > 1 ? WColour : CColour;
            }

            if (_damageFlash > 0f)
                _damageFlash = Mathf.Max(0f, _damageFlash - Time.deltaTime * 4f);
        }

        // ── UI factory helpers ────────────────────────────────────────────────

        private static Image MakeImage(RectTransform parent, string name,
            Vector2 anchoredPos, Vector2 size, Color color)
        {
            var go  = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            var rt  = img.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta        = size;
            return img;
        }

        private static TextMeshProUGUI MakeTMP(RectTransform parent, string name,
            Vector2 anchoredPos, Vector2 size, string text, float fontSize,
            Color color, TextAlignmentOptions alignment)
        {
            var go  = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text; tmp.fontSize = fontSize;
            tmp.color = color; tmp.alignment = alignment;
            var rt  = tmp.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta        = size;
            return tmp;
        }
    }
}
