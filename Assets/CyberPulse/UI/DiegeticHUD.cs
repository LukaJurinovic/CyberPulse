using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CyberPulse.Player;
using CyberPulse.Systems;
using CyberPulse.Weapons;

namespace CyberPulse.UI
{
    /// <summary>
    /// World-space HUD parented to the CameraPivot — floats in 3D space at a
    /// fixed offset in front of the player's view, replacing the OnGUI overlay.
    ///
    /// Layout (small holographic panel, lower-left corner of view):
    ///   ┌──────────────────────────────────┐
    ///   │  PHASE          ▐▐▐▐▐░░░ TRACE  │
    ///   │  HP  ■■■■■■░░  80    AMO 28/30  │
    ///   │                   SCORE  012500  │
    ///   └──────────────────────────────────┘
    /// </summary>
    public class DiegeticHUD : MonoBehaviour
    {
        // Wired by builder
        [SerializeField] private PlayerStats  _playerStats;
        [SerializeField] private WeaponHolder _weaponHolder;
        [SerializeField] private Transform    _cameraPivot;

        // Canvas config
        [Header("Canvas positioning (relative to camera pivot)")]
        [SerializeField] private Vector3 _canvasOffset    = new Vector3(-0.20f, -0.175f, 0.42f);
        [SerializeField] private float   _canvasWidth     = 0.38f;   // world-space metres
        [SerializeField] private float   _canvasHeight    = 0.11f;

        // Runtime TMP refs — created in Start
        private TextMeshProUGUI _txtPhase;
        private TextMeshProUGUI _txtHP;
        private TextMeshProUGUI _txtAmmo;
        private TextMeshProUGUI _txtScore;
        private Image           _traceBar;
        private Image           _hpBar;

        // Damage flash
        private float _damageFlash;

        // ── Colour palette ────────────────────────────────────────────────────
        private static readonly Color CColour = new Color(0f,    0.96f, 1f,    1f);   // cyan
        private static readonly Color WColour = new Color(1f,    0.85f, 0.1f,  1f);   // warn yellow
        private static readonly Color DColour = new Color(1f,    0.25f, 0.1f,  1f);   // danger red
        private static readonly Color DimCol  = new Color(0.15f, 0.18f, 0.25f, 0.85f);// panel bg
        private static readonly Color TraceNorm   = new Color(0f,  0.9f,  1f,   1f);
        private static readonly Color TraceCrit   = new Color(1f,  0.3f,  0.1f, 1f);

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
            // Root canvas GO parented to camera pivot
            var canvasGO = new GameObject("DiegeticCanvas");
            canvasGO.transform.SetParent(_cameraPivot, false);
            canvasGO.transform.localPosition = _canvasOffset;
            canvasGO.transform.localRotation = Quaternion.identity;

            var canvas       = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var rt = canvasGO.GetComponent<RectTransform>();
            // 1 pixel = 0.001 world units → 400×120 px = 0.40m × 0.12m
            const float PixPerMetre = 1000f;
            rt.sizeDelta  = new Vector2(_canvasWidth * PixPerMetre, _canvasHeight * PixPerMetre);
            rt.localScale = Vector3.one / PixPerMetre;

            // Semi-transparent dark background panel — stretch to fill canvas
            var bg   = MakeImage(rt, "BG", Vector2.zero, rt.sizeDelta, DimCol);
            var bgRT = bg.rectTransform;
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;

            float W = rt.sizeDelta.x;
            float H = rt.sizeDelta.y;

            // Row heights (pixels)
            float rowTop = H * 0.72f;   // centre-y of top row
            float rowBot = H * 0.27f;   // centre-y of bottom row

            // ── Top row ───────────────────────────────────────────────────────
            // Phase label
            _txtPhase = MakeTMP(rt, "Phase", new Vector2(W * 0.13f, rowTop),
                new Vector2(W * 0.26f, H * 0.4f), "INFILTRATE", 26, CColour, TextAlignmentOptions.Left);

            // Trace bar background + fill
            var traceBase = MakeImage(rt, "TraceBG",
                new Vector2(W * 0.72f, rowTop), new Vector2(W * 0.44f, 14f),
                new Color(0.1f, 0.12f, 0.18f, 1f));
            _traceBar = MakeImage(rt, "TraceFill",
                new Vector2(0f, 0f), new Vector2(W * 0.44f, 14f),
                TraceNorm);
            _traceBar.transform.SetParent(traceBase.transform, false);
            var trt = _traceBar.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0, 0);
            trt.anchorMax = new Vector2(1, 1);
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            _traceBar.type = Image.Type.Filled;
            _traceBar.fillMethod = Image.FillMethod.Horizontal;

            // Trace label
            MakeTMP(rt, "TraceLabel", new Vector2(W * 0.72f, rowTop + 14f),
                new Vector2(W * 0.44f, 18f), "TRACE", 18, new Color(0.6f, 0.7f, 0.8f, 1f),
                TextAlignmentOptions.Right);

            // ── Bottom row ────────────────────────────────────────────────────
            // HP bar background + fill
            var hpBase = MakeImage(rt, "HPBG",
                new Vector2(W * 0.18f, rowBot), new Vector2(W * 0.28f, 12f),
                new Color(0.1f, 0.12f, 0.18f, 1f));
            _hpBar = MakeImage(rt, "HPFill",
                Vector2.zero, new Vector2(W * 0.28f, 12f), CColour);
            _hpBar.transform.SetParent(hpBase.transform, false);
            var hrt = _hpBar.GetComponent<RectTransform>();
            hrt.anchorMin = new Vector2(0, 0);
            hrt.anchorMax = new Vector2(1, 1);
            hrt.offsetMin = hrt.offsetMax = Vector2.zero;
            _hpBar.type = Image.Type.Filled;
            _hpBar.fillMethod = Image.FillMethod.Horizontal;

            MakeTMP(rt, "HPLabel", new Vector2(W * 0.03f, rowBot),
                new Vector2(W * 0.08f, H * 0.4f), "HP", 22,
                new Color(0.6f, 0.7f, 0.8f, 1f), TextAlignmentOptions.Left);
            _txtHP = MakeTMP(rt, "HPVal", new Vector2(W * 0.33f, rowBot),
                new Vector2(W * 0.08f, H * 0.4f), "100", 26, CColour, TextAlignmentOptions.Left);

            // Ammo
            MakeTMP(rt, "AmmoLabel", new Vector2(W * 0.58f, rowBot),
                new Vector2(W * 0.10f, H * 0.4f), "AMO", 22,
                new Color(0.6f, 0.7f, 0.8f, 1f), TextAlignmentOptions.Left);
            _txtAmmo = MakeTMP(rt, "AmmoVal", new Vector2(W * 0.72f, rowBot),
                new Vector2(W * 0.24f, H * 0.4f), "30/30", 26, WColour, TextAlignmentOptions.Left);

            // Score
            _txtScore = MakeTMP(rt, "Score", new Vector2(W * 0.72f, rowBot - 20f),
                new Vector2(W * 0.26f, 22f), "0", 22, CColour, TextAlignmentOptions.Right);
        }

        // ── Runtime refresh ───────────────────────────────────────────────────

        private void RefreshStats()
        {
            // HP
            if (_playerStats != null)
            {
                float hpNorm = _playerStats.CurrentHealth / (float)_playerStats.MaxHealth;
                _txtHP.text = _playerStats.CurrentHealth.ToString();
                _hpBar.fillAmount  = hpNorm;
                _hpBar.color       = hpNorm > 0.4f ? CColour : DColour;
                _txtHP.color       = hpNorm > 0.4f ? CColour : DColour;
            }

            // Ammo
            if (_weaponHolder != null && _weaponHolder.ActiveWeapon != null)
            {
                var w = _weaponHolder.ActiveWeapon;
                _txtAmmo.text  = $"{w.CurrentAmmo}/{w.ReserveAmmo}";
                _txtAmmo.color = w.CurrentAmmo > 0 ? WColour : DColour;
            }

            // Trace meter
            if (TraceMeter.Instance != null)
            {
                float tn = TraceMeter.Instance.Normalized;
                _traceBar.fillAmount = tn;
                _traceBar.color      = tn < 0.8f ? TraceNorm : TraceCrit;
            }

            // Phase
            if (GameManager.Instance != null)
                _txtPhase.text = GameManager.Instance.CurrentPhase.ToString().ToUpper();

            // Score
            if (ScoreManager.Instance != null)
            {
                _txtScore.text = ScoreManager.Instance.Score.ToString("D6");

                // Combo indicator — tint score yellow at >1x
                if (ScoreManager.Instance.ComboCount > 1)
                    _txtScore.text += $"  x{ScoreManager.Instance.ComboCount}";
                _txtScore.color = ScoreManager.Instance.ComboCount > 1 ? WColour : CColour;
            }

            // Damage flash — tint background red briefly
            if (_damageFlash > 0f)
            {
                _damageFlash = Mathf.Max(0f, _damageFlash - Time.deltaTime * 4f);
            }
        }

        // ── UI factory helpers ────────────────────────────────────────────────

        private static Image MakeImage(RectTransform parent, string name,
            Vector2 anchoredPos, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            var rt = img.rectTransform;
            rt.anchorMin     = new Vector2(0.5f, 0.5f);
            rt.anchorMax     = new Vector2(0.5f, 0.5f);
            rt.pivot         = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta     = size;
            return img;
        }

        private static TextMeshProUGUI MakeTMP(RectTransform parent, string name,
            Vector2 anchoredPos, Vector2 size, string text, float fontSize,
            Color color, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text      = text;
            tmp.fontSize  = fontSize;
            tmp.color     = color;
            tmp.alignment = alignment;
            var rt = tmp.rectTransform;
            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta        = size;
            return tmp;
        }
    }
}
