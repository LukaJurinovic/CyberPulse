using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CyberPulse.Systems
{
    public enum GamePhase { Infiltrate, Siphon, Purge, Extract }

    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Fail State")]
        [SerializeField] private float _failRestartDelay = 2f;
        [SerializeField] private GameObject _failOverlay;

        [Header("Win State")]
        [SerializeField] private float _winRestartDelay = 3f;
        [SerializeField] private GameObject _winOverlay;

        public GamePhase CurrentPhase { get; private set; } = GamePhase.Infiltrate;

        public event Action<GamePhase> OnPhaseChanged;
        public event Action OnFail;
        public event Action OnWin;

        private bool _gameOver;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void SetPhase(GamePhase phase)
        {
            if (CurrentPhase == phase) return;
            CurrentPhase = phase;
            OnPhaseChanged?.Invoke(phase);
        }

        public void TriggerFailState()
        {
            if (_gameOver) return;
            _gameOver = true;
            OnFail?.Invoke();
            if (_failOverlay != null) _failOverlay.SetActive(true);
            StartCoroutine(ReloadAfterDelay(_failRestartDelay));
        }

        public void TriggerWinState()
        {
            if (_gameOver) return;
            _gameOver = true;
            OnWin?.Invoke();
            if (_winOverlay != null) _winOverlay.SetActive(true);
            StartCoroutine(ReloadAfterDelay(_winRestartDelay));
        }

        // Unscaled time so the scene reloads even if timeScale was zeroed by a kill-cam effect.
        private IEnumerator ReloadAfterDelay(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
