using UnityEngine;
using CyberPulse.Player;
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

        private void Awake()
        {
            _pixel = new Texture2D(1, 1);
            _pixel.SetPixel(0, 0, Color.white);
            _pixel.Apply();
        }

        private void Update()
        {
            float speedRatio = _controller != null
                ? Mathf.Clamp01(_controller.CurrentSpeed / _controller.MaxHorizontalSpeed)
                : 0f;

            bool airborne = _controller != null && !_controller.IsGrounded;
            float targetExtra = speedRatio * _maxExtraGap + (airborne ? _maxExtraGap * 0.5f : 0f);
            float targetGap = _baseGap + targetExtra;

            _currentGap = Mathf.Lerp(_currentGap, targetGap, Time.deltaTime * _gapSmoothing);
        }

        private void OnGUI()
        {
            if (_pixel == null) return;

            GUI.color = _color;

            float cx = Screen.width  * 0.5f;
            float cy = Screen.height * 0.5f;
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
