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

        [SerializeField] private ArenaLayer[] _layers;

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

        private void HandleLayerCleared(ArenaLayer layer)
        {
            if (layer != ActiveLayer) return;

            if (layer.ExitDoor != null)
                layer.ExitDoor.Unlock();
            else
                GameManager.Instance?.TriggerWinState();
        }

        private void HandleDoorPassed()
        {
            var current = ActiveLayer;
            if (current?.ExitDoor != null) current.ExitDoor.Lock();

            if (ActiveIndex + 1 < LayerCount)
            {
                ActiveIndex++;
                OnActiveLayerChanged?.Invoke(ActiveIndex);
            }
        }
    }
}
