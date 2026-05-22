using UnityEngine;
using UnityEngine.InputSystem;
using CyberPulse.Player;

namespace CyberPulse.UI
{
    /// <summary>
    /// Immediate-mode debug overlay that displays live movement state.
    /// Uses <c>OnGUI()</c> — no Canvas setup required. Works the moment you enter Play Mode.
    /// Toggle visibility with <b>F1</b> (new Input System Keyboard).
    /// </summary>
    public class MovementDebugUI : MonoBehaviour
    {
        [SerializeField] private PlayerController _controller;
        [SerializeField] private DashAbility _dash;

        private bool _visible = true;

        // Cached styles — built once on first OnGUI call so GUISkin is available.
        private GUIStyle _boxStyle;
        private GUIStyle _labelStyle;
        private bool _stylesReady;

        private const float PanelX = 10f;
        private const float PanelY = 10f;
        private const float Padding = 10f;

        // ──────────────────────────────────────────────────────────────────────
        // Toggle
        // ──────────────────────────────────────────────────────────────────────

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current[Key.F1].wasPressedThisFrame)
                _visible = !_visible;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Rendering
        // ──────────────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            if (!_visible) return;

            EnsureStyles();

            string text = BuildText();
            GUIContent content = new GUIContent(text);
            Vector2 size = _labelStyle.CalcSize(content);

            Rect boxRect = new Rect(PanelX, PanelY, size.x + Padding * 2f, size.y + Padding * 2f);
            Rect textRect = new Rect(boxRect.x + Padding, boxRect.y + Padding, size.x, size.y);

            GUI.Box(boxRect, GUIContent.none, _boxStyle);
            GUI.Label(textRect, content, _labelStyle);
        }

        private string BuildText()
        {
            float speed    = _controller.CurrentSpeed;
            float maxSpeed = _controller.MaxHorizontalSpeed;
            Vector3 vel    = _controller.Velocity;

            string state = GetStateLabel();

            string dashStatus = _dash.CanDash
                ? "<color=#00F5FF>READY</color>"
                : $"cooldown: {_dash.CooldownRemaining:F1}s";

            string bufferStatus = _controller.JumpBufferRemaining > 0f ? "<color=#00F5FF>active</color>" : "–";
            string coyoteStatus = _controller.CoyoteTimeRemaining  > 0f ? "<color=#00F5FF>active</color>" : "–";

            int jumpsLeft = _controller.JumpsRemaining;
            int jumpsMax  = _controller.MaxJumps;
            string jumpDots = "";
            for (int i = 0; i < jumpsMax; i++)
                jumpDots += i < jumpsLeft ? "<color=#00F5FF>■</color>" : "<color=#444444>□</color>";

            return
                $"SPEED       {speed,5:F1} / {maxSpeed:F0}  m/s\n" +
                $"STATE       {state}\n" +
                $"VELOCITY    X:{vel.x,6:F2}  Y:{vel.y,6:F2}  Z:{vel.z,6:F2}\n" +
                $"DASH        {dashStatus}\n" +
                $"JUMPS       {jumpDots}  ({jumpsLeft}/{jumpsMax})\n" +
                $"JUMP BUFF   {bufferStatus}\n" +
                $"COYOTE      {coyoteStatus}";
        }

        private string GetStateLabel()
        {
            if (_controller.IsDashing)    return "<color=#FF6B00>DASHING</color>";
            if (_controller.IsWallSliding) return "<color=#A78BFA>WALL SLIDE</color>";
            if (_controller.IsGrounded)    return "<color=#4ADE80>GROUNDED</color>";
            return "<color=#60A5FA>AIRBORNE</color>";
        }

        // ──────────────────────────────────────────────────────────────────────
        // Style initialisation (deferred until GUISkin is available)
        // ──────────────────────────────────────────────────────────────────────

        private void EnsureStyles()
        {
            if (_stylesReady) return;

            _boxStyle = new GUIStyle(GUI.skin.box);
            _boxStyle.normal.background = MakeSolidTex(new Color(0f, 0f, 0f, 0.75f));

            _labelStyle = new GUIStyle(GUI.skin.label);
            _labelStyle.font = Font.CreateDynamicFontFromOSFont(
                new[] { "Courier New", "Consolas", "Menlo", "Lucida Console" }, 14);
            _labelStyle.fontSize = 14;
            _labelStyle.normal.textColor = new Color(0f, 0.96f, 1f);
            _labelStyle.richText = true;
            _labelStyle.wordWrap = false;

            _stylesReady = true;
        }

        private static Texture2D MakeSolidTex(Color color)
        {
            Texture2D tex = new Texture2D(2, 2);
            tex.SetPixels(new[] { color, color, color, color });
            tex.Apply();
            return tex;
        }
    }
}
