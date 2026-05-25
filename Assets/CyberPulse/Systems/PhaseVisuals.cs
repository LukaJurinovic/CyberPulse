using UnityEngine;
using UnityEngine.Rendering;

namespace CyberPulse.Systems
{
    /// <summary>
    /// Reacts to GameManager phase changes with ambient light shifts and an
    /// optional Post-Processing Volume blend. Handles the cyan→red color inversion
    /// on Extract without requiring custom shaders.
    /// </summary>
    public class PhaseVisuals : MonoBehaviour
    {
        [Header("Extract Phase")]
        [SerializeField] private Color _extractAmbient  = new Color(0.12f, 0.01f, 0.01f);
        [SerializeField] private Volume _extractVolume;
        [SerializeField] private float  _volumeLerpSpeed = 2.5f;

        [Header("Purge Phase")]
        [SerializeField] private Color _purgeAmbient = new Color(0.06f, 0.01f, 0.02f);

        private Color _defaultAmbient;
        private float _targetVolumeWeight;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            _defaultAmbient = RenderSettings.ambientLight;
        }

        private void Start()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnPhaseChanged += OnPhaseChanged;
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnPhaseChanged -= OnPhaseChanged;
        }

        private void Update()
        {
            if (_extractVolume == null) return;
            _extractVolume.weight = Mathf.Lerp(
                _extractVolume.weight, _targetVolumeWeight, Time.deltaTime * _volumeLerpSpeed);
        }

        // ── Phase reactions ───────────────────────────────────────────────────

        private void OnPhaseChanged(GamePhase phase)
        {
            switch (phase)
            {
                case GamePhase.Infiltrate:
                case GamePhase.Siphon:
                    RenderSettings.ambientLight = _defaultAmbient;
                    _targetVolumeWeight = 0f;
                    break;

                case GamePhase.Purge:
                    RenderSettings.ambientLight = _purgeAmbient;
                    _targetVolumeWeight = 0f;
                    break;

                case GamePhase.Extract:
                    RenderSettings.ambientLight = _extractAmbient;
                    _targetVolumeWeight = 1f;
                    break;
            }
        }
    }
}
