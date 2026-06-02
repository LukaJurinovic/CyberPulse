using UnityEngine;
using CyberPulse.Systems;

namespace CyberPulse.Player
{
    /// <summary>
    /// Bridges PlayerStats.OnDeath to GameManager.TriggerFailState.
    /// Kept separate so PlayerStats stays engine-agnostic.
    /// </summary>
    [RequireComponent(typeof(PlayerStats))]
    public class PlayerDeathHandler : MonoBehaviour
    {
        private void Awake()
        {
            GetComponent<PlayerStats>().OnDeath += () => GameManager.Instance?.TriggerFailState();
        }
    }
}
