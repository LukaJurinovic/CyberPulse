using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using CyberPulse.Systems;
using CyberPulse.Weapons;

namespace CyberPulse.UI
{
    public class PauseMenu : MonoBehaviour
    {
        private const string MainMenuScene = "MainMenu";

        private bool _isPaused;
        private WeaponHolder _weaponHolder;

        private bool _stylesBuilt;
        private GUIStyle _titleStyle;
        private GUIStyle _buttonStyle;

        private void Start()
        {
            var holders = FindObjectsByType<WeaponHolder>(FindObjectsInactive.Exclude);
            if (holders.Length > 0) _weaponHolder = holders[0];
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (!_isPaused && GameManager.Instance != null && GameManager.Instance.IsGameOver)
                    return;
                SetPaused(!_isPaused);
            }
        }

        private void SetPaused(bool paused)
        {
            _isPaused = paused;
            Time.timeScale       = paused ? 0f : 1f;
            AudioListener.pause  = paused;

            Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible   = paused;

            if (_weaponHolder != null)
            {
                if (paused) _weaponHolder.ActiveWeapon?.StopFireAudio();
                _weaponHolder.enabled = !paused;
            }
        }

        private void OnGUI()
        {
            if (!_isPaused) return;
            if (!_stylesBuilt) BuildStyles();

            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.78f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = prev;

            float w = 300f, h = 200f;
            float x = (Screen.width  - w) * 0.5f;
            float y = (Screen.height - h) * 0.5f;

            GUI.Label(new Rect(x, y, w, 56f), "// PAUSED", _titleStyle);

            if (GUI.Button(new Rect(x + 30f, y + 74f,  w - 60f, 46f), "[ RESUME ]",    _buttonStyle))
                SetPaused(false);

            if (GUI.Button(new Rect(x + 30f, y + 136f, w - 60f, 46f), "[ QUIT TO MENU ]", _buttonStyle))
                QuitToMenu();
        }

        private void QuitToMenu()
        {
            Time.timeScale      = 1f;
            AudioListener.pause = false;
            Cursor.lockState    = CursorLockMode.None;
            Cursor.visible      = true;
            SceneManager.LoadScene(MainMenuScene);
        }

        private void BuildStyles()
        {
            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 30,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            _titleStyle.normal.textColor = new Color(0f, 0.9f, 1f);

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize  = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            _buttonStyle.normal.textColor = new Color(0f, 0.85f, 1f);
            _buttonStyle.hover.textColor  = Color.white;
            _buttonStyle.active.textColor = Color.white;

            _stylesBuilt = true;
        }
    }
}
