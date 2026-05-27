using UnityEngine;
using CyberPulse.Systems;

namespace CyberPulse.World
{
    /// <summary>
    /// Selects one of three cover layouts at runtime based on SongProfile.EnergyVariance.
    /// PlayableLevelBuilder creates all three as deactivated children; this activates one
    /// when song analysis completes (or falls back to Standard if no song is loaded).
    ///
    ///   EnergyVariance  &lt; 0.3  →  Open     (4 blocks, lots of space to kite)
    ///   EnergyVariance  0.3-0.6 → Standard  (10 blocks, balanced cover)
    ///   EnergyVariance ≥ 0.6  →  Dense    (16 blocks, corridor pressure)
    /// </summary>
    public class ArenaLayoutController : MonoBehaviour
    {
        [SerializeField] private GameObject _layoutOpen;
        [SerializeField] private GameObject _layoutStandard;
        [SerializeField] private GameObject _layoutDense;

        private void Start()
        {
            if (SongAnalyzer.Instance == null) { ActivateLayout(1); return; }

            if (SongAnalyzer.Instance.IsAnalyzed)
                ApplyProfile(SongAnalyzer.Instance.Profile);
            else
                SongAnalyzer.Instance.OnAnalysisComplete += ApplyProfile;
        }

        private void OnDestroy()
        {
            if (SongAnalyzer.Instance != null)
                SongAnalyzer.Instance.OnAnalysisComplete -= ApplyProfile;
        }

        private void ApplyProfile(SongProfile profile)
        {
            if      (profile.EnergyVariance < 0.3f) ActivateLayout(0);
            else if (profile.EnergyVariance < 0.6f) ActivateLayout(1);
            else                                     ActivateLayout(2);
        }

        private void ActivateLayout(int index)
        {
            if (_layoutOpen     != null) _layoutOpen.SetActive(index == 0);
            if (_layoutStandard != null) _layoutStandard.SetActive(index == 1);
            if (_layoutDense    != null) _layoutDense.SetActive(index == 2);
            Debug.Log($"[ArenaLayoutController] Layout {index} active (variance tier).");
        }
    }
}
