using UnityEngine;
using CyberPulse.Systems;

namespace CyberPulse.UI
{
    /// <summary>
    /// Full-screen end-state overlay. Activated by GameManager on win or fail.
    /// Uses OnGUI to match the rest of the HUD style.
    /// </summary>
    public class GameOverlay : MonoBehaviour
    {
        [SerializeField] private string _headline      = "GAME OVER";
        [SerializeField] private Color  _headlineColor = Color.white;
        [SerializeField] private Color  _bgColor       = new Color(0f, 0f, 0f, 0.78f);

        private Texture2D _pixel;
        private GUIStyle  _headStyle;
        private GUIStyle  _subStyle;
        private GUIStyle  _hintStyle;
        private float     _activatedAt = -1f;

        private void Awake()
        {
            _pixel = new Texture2D(1, 1);
            _pixel.SetPixel(0, 0, Color.white);
            _pixel.Apply();
        }

        private void OnEnable()
        {
            _activatedAt = Time.unscaledTime;
        }

        // Laid out against this virtual height and scaled uniformly to the real
        // resolution so the overlay matches the rest of the HUD across resolutions.
        private const float ReferenceHeight = 720f;

        private void OnGUI()
        {
            float     scale      = Screen.height / ReferenceHeight;
            Matrix4x4 prevMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            float sw = Screen.width  / scale;
            float sh = Screen.height / scale;

            float alpha = _activatedAt < 0f ? 1f
                : Mathf.Clamp01((Time.unscaledTime - _activatedAt) / 0.4f);

            GUI.color = new Color(_bgColor.r, _bgColor.g, _bgColor.b, _bgColor.a * alpha);
            GUI.DrawTexture(new Rect(0, 0, sw, sh), _pixel);
            GUI.color = Color.white;

            EnsureStyles();

            float cy = sh * 0.40f;

            float pulse = alpha >= 1f ? 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 2.5f) : alpha;
            _headStyle.normal.textColor = new Color(
                _headlineColor.r, _headlineColor.g, _headlineColor.b, pulse);
            GUI.Label(new Rect(sw * 0.5f - 240f, cy, 480f, 90f), _headline, _headStyle);

            if (alpha < 0.6f) { GUI.matrix = prevMatrix; return; }

            float sub = Mathf.Clamp01((alpha - 0.6f) / 0.4f);

            var sm = ScoreManager.Instance;
            if (sm != null)
            {
                _subStyle.normal.textColor = new Color(1f, 0.85f, 0.1f, sub);
                GUI.Label(new Rect(sw * 0.5f - 160f, cy + 94f, 320f, 38f),
                    $"SCORE  {sm.Score:D6}", _subStyle);
            }

            _hintStyle.normal.textColor = new Color(0.55f, 0.65f, 0.75f, sub * 0.85f);
            GUI.Label(new Rect(sw * 0.5f - 120f, cy + 140f, 240f, 24f),
                "Restarting...", _hintStyle);

            GUI.matrix = prevMatrix;
        }

        private void EnsureStyles()
        {
            if (_headStyle != null) return;
            _headStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 52,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            _subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            _hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 13,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
            };
        }
    }
}
