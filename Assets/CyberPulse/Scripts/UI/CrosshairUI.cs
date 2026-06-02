using UnityEngine;
using CyberPulse.Player;
using CyberPulse.Systems;
using CyberPulse.Weapons;

namespace CyberPulse.UI
{
    /// <summary>
    /// Immediate-mode dynamic crosshair. Uses OnGUI — no Canvas required.
    /// Four lines + center dot. Gap expands based on player speed and airborne state.
    /// </summary>
    public class CrosshairUI : MonoBehaviour
    {
        [SerializeField] private PlayerController _controller;
        [SerializeField] private WeaponHolder _weaponHolder;

        [Header("Appearance")]
        [SerializeField] private Color _color = new Color(0f, 0.96f, 1f, 0.9f);
        [SerializeField] private float _lineLength = 10f;
        [SerializeField] private float _lineWidth = 2f;
        [SerializeField] private float _baseGap = 4f;
        [SerializeField] private float _maxExtraGap = 20f;
        [SerializeField] private float _gapSmoothing = 8f;

        private float _currentGap;
        private Texture2D _pixel;

        // Beat pulse state
        private float _beatPulse;        // 0-1, spikes on OnBeat
        private float _beatKillFlash;    // 0-1, spikes on confirmed on-beat kill

        private static readonly Color CyanBeat  = new Color(0.5f, 1f,   1f,   1f);
        private static readonly Color WhiteFlash = new Color(1f,   1f,   1f,   1f);
        private static readonly Color OffRhythm  = new Color(0.8f, 0.45f, 0.1f, 0.7f);

        private void Awake()
        {
            _pixel = new Texture2D(1, 1);
            _pixel.SetPixel(0, 0, Color.white);
            _pixel.Apply();
        }

        private void Start()
        {
            if (BeatClock.Instance != null)
                BeatClock.Instance.OnBeat += HandleBeat;

            if (BeatReactor.Instance != null)
                BeatReactor.Instance.OnBeatKill += HandleBeatKill;
        }

        private void OnDestroy()
        {
            if (BeatClock.Instance != null)
                BeatClock.Instance.OnBeat -= HandleBeat;

            if (BeatReactor.Instance != null)
                BeatReactor.Instance.OnBeatKill -= HandleBeatKill;
        }

        private void HandleBeat()          => _beatPulse    = 1f;
        private void HandleBeatKill()      => _beatKillFlash = 1f;

        private void Update()
        {
            float speedRatio = _controller != null
                ? Mathf.Clamp01(_controller.CurrentSpeed / _controller.MaxHorizontalSpeed)
                : 0f;

            bool  airborne   = _controller != null && !_controller.IsGrounded;
            float targetExtra = speedRatio * _maxExtraGap + (airborne ? _maxExtraGap * 0.5f : 0f);
            float targetGap  = _baseGap + targetExtra;
            _currentGap      = Mathf.Lerp(_currentGap, targetGap, Time.deltaTime * _gapSmoothing);

            // Decay beat pulse effects
            _beatPulse    = Mathf.Max(0f, _beatPulse    - Time.deltaTime * 8f);
            _beatKillFlash = Mathf.Max(0f, _beatKillFlash - Time.deltaTime * 6f);
        }

        private void OnGUI()
        {
            if (_pixel == null) return;

            // Blend base colour toward beat-pulse effects.
            bool offRhythm = BeatReactor.Instance != null && BeatReactor.Instance.IsOffRhythm;

            Color crosshairColor;
            if (_beatKillFlash > 0f)
                crosshairColor = Color.Lerp(_color, WhiteFlash, _beatKillFlash);
            else if (_beatPulse > 0f)
                crosshairColor = Color.Lerp(_color, CyanBeat, _beatPulse);
            else if (offRhythm)
                crosshairColor = OffRhythm;
            else
                crosshairColor = _color;

            GUI.color = crosshairColor;

            float cx  = Screen.width  * 0.5f;
            float cy  = Screen.height * 0.5f;
            float gap = _currentGap;
            float len = _lineLength;
            float w   = _lineWidth;

            // Center dot
            DrawRect(cx - 1f, cy - 1f, 2f, 2f);

            // Top
            DrawRect(cx - w * 0.5f, cy - gap - len, w, len);
            // Bottom
            DrawRect(cx - w * 0.5f, cy + gap, w, len);
            // Left
            DrawRect(cx - gap - len, cy - w * 0.5f, len, w);
            // Right
            DrawRect(cx + gap, cy - w * 0.5f, len, w);

            GUI.color = Color.white;
        }

        private void DrawRect(float x, float y, float w, float h)
        {
            GUI.DrawTexture(new Rect(x, y, w, h), _pixel);
        }
    }
}
