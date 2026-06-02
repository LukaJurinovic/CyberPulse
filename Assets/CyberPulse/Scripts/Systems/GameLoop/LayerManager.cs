using System;
using UnityEngine;
using CyberPulse.World;

namespace CyberPulse.Systems
{
    /// <summary>
    /// Coordinates the vertical layered progression (plan Bucket B).
    ///
    /// Owns the ordered <see cref="ArenaLayer"/> stack (index 0 = ground, where the
    /// player starts). The "active" layer is the one the player is fighting on.
    ///
    ///   • When the active layer reports cleared, its exit door is unlocked.
    ///   • When the player walks through that door, the active layer advances and the
    ///     door re-seals behind them so they can't fall back into a cleared area.
    ///   • Clearing the top layer (no exit door) triggers the GameManager win.
    ///
    /// WaveDirector reads <see cref="ActiveLayer"/> each frame to spawn into the right
    /// arena, so this manager only needs to drive layer state — not the waves directly.
    /// </summary>
    public class LayerManager : MonoBehaviour
    {
        public static LayerManager Instance { get; private set; }

        [SerializeField] private ArenaLayer[] _layers;   // ascent order; [0] is the start layer

        public int        ActiveIndex { get; private set; }
        public int        LayerCount  => _layers?.Length ?? 0;
        public ArenaLayer ActiveLayer =>
            _layers != null && ActiveIndex >= 0 && ActiveIndex < _layers.Length
                ? _layers[ActiveIndex]
                : null;

        /// <summary>Fires when the active layer changes (including the initial layer 0). Arg = new index.</summary>
        public event Action<int> OnActiveLayerChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            if (_layers != null)
            {
                foreach (var layer in _layers)
                {
                    if (layer == null) continue;
                    layer.OnCleared += HandleLayerCleared;
                    if (layer.ExitDoor != null)
                        layer.ExitDoor.OnPlayerPassed += HandleDoorPassed;
                }
            }

            // Kick off: layer 0 is active from the start.
            OnActiveLayerChanged?.Invoke(ActiveIndex);
        }

        private void OnDestroy()
        {
            if (_layers != null)
            {
                foreach (var layer in _layers)
                {
                    if (layer == null) continue;
                    layer.OnCleared -= HandleLayerCleared;
                    if (layer.ExitDoor != null)
                        layer.ExitDoor.OnPlayerPassed -= HandleDoorPassed;
                }
            }
            if (Instance == this) Instance = null;
        }

        // ── Layer flow ──────────────────────────────────────────────────────────

        private void HandleLayerCleared(ArenaLayer layer)
        {
            // Only the active layer's completion matters for progression.
            if (layer != ActiveLayer) return;

            if (layer.ExitDoor != null)
                layer.ExitDoor.Unlock();           // let the player ascend
            else
                GameManager.Instance?.TriggerWinState();  // top layer cleared → run complete
        }

        // Only the active layer's door can be unlocked, so a pass-through always means
        // "advance from the current active layer".
        private void HandleDoorPassed()
        {
            var current = ActiveLayer;
            if (current?.ExitDoor != null) current.ExitDoor.Lock();   // re-seal behind the player

            if (ActiveIndex + 1 < LayerCount)
            {
                ActiveIndex++;
                OnActiveLayerChanged?.Invoke(ActiveIndex);
            }
        }
    }
}
