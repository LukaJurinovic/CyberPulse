using System.Collections.Generic;
using UnityEngine;

namespace CyberPulse.Systems
{
    /// <summary>
    /// Vibrates interior cover/platform geometry to the beat. Listens to
    /// BeatClock.OnBeat for impulse triggers and scales amplitude by
    /// AudioAnalyzer.BassAmplitude. Decays back to rest each frame.
    ///
    /// Items are gathered lazily so the script can be attached before
    /// ProceduralArenaGenerator has spawned its children.
    /// </summary>
    public class WorldBeatShake : MonoBehaviour
    {
        [SerializeField] private float _baseAmplitude = 0.18f;   // metres of XZ jitter per beat
        [SerializeField] private float _verticalBias  = 0.4f;    // less vertical than horizontal
        [SerializeField] private float _scaleBoost    = 0.18f;   // brief scale-up on heavy beats — clearly visible breathing
        [SerializeField] private float _decaySpeed    = 5f;
        [SerializeField] private float _bassGain      = 8f;      // BassAmplitude → 0-1 mapping
        [SerializeField] private float _elevatedSkipY = 2.5f;    // Skip blocks above this height (Tower/Platform tops must be stable landings)

        private struct Item
        {
            public Transform t;
            public Vector3   basePos;
            public Vector3   baseScale;
            public Vector3   offset;
            public float     scaleBoost;
        }

        private readonly List<Item> _items = new();
        private bool _subscribed;

        private void OnEnable()
        {
            if (BeatClock.Instance != null)
            {
                BeatClock.Instance.OnBeat += OnBeat;
                _subscribed = true;
            }
        }

        private void Start()
        {
            // BeatClock may not have existed during OnEnable (script execution order).
            if (!_subscribed && BeatClock.Instance != null)
            {
                BeatClock.Instance.OnBeat += OnBeat;
                _subscribed = true;
            }
        }

        private void OnDisable()
        {
            if (_subscribed && BeatClock.Instance != null)
                BeatClock.Instance.OnBeat -= OnBeat;
            _subscribed = false;
        }

        private void OnBeat()
        {
            RefreshItems();

            float energy = AudioAnalyzer.Instance != null
                ? Mathf.Clamp01(AudioAnalyzer.Instance.BassAmplitude * _bassGain)
                : 0.5f;

            float amp     = _baseAmplitude * (0.35f + energy * 1.65f);
            float ampVert = amp * _verticalBias;
            float boost   = _scaleBoost   * energy;

            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                if (item.t == null) continue;
                item.offset = new Vector3(
                    Random.Range(-amp,     amp),
                    Random.Range(-ampVert, ampVert),
                    Random.Range(-amp,     amp));
                item.scaleBoost = boost;
                _items[i] = item;
            }
        }

        private void Update()
        {
            if (_items.Count == 0) return;

            float k = Time.deltaTime * _decaySpeed;
            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                if (item.t == null) continue;
                item.offset     = Vector3.Lerp(item.offset,     Vector3.zero, k);
                item.scaleBoost = Mathf.Lerp (item.scaleBoost, 0f,            k);
                item.t.position   = item.basePos + item.offset;
                item.t.localScale = item.baseScale * (1f + item.scaleBoost);
                _items[i] = item;
            }
        }

        /// <summary>
        /// Walk the children of this transform and gather cover blocks. Skips
        /// JumpPad subtrees so we don't move trigger volumes around. Cheap to call
        /// every beat — already-tracked items are de-duplicated.
        /// </summary>
        private void RefreshItems()
        {
            CollectChildren(transform);
        }

        private void CollectChildren(Transform parent)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i);

                // JumpPad blocks have a Trigger child; moving them would re-fire
                // the volume erratically. Leave them stationary.
                if (c.name == "JumpPad") continue;

                if (c.name == "Cover")
                {
                    // Elevated structures (Towers / raised Platforms) must stay still — the player
                    // lands on their tops via JumpPads and a shaking surface would feel awful, plus
                    // it would prevent NavMeshObstacle carving from settling.
                    if (c.position.y > _elevatedSkipY) continue;
                    if (ContainsTransform(c)) continue;
                    _items.Add(new Item
                    {
                        t         = c,
                        basePos   = c.position,
                        baseScale = c.localScale,
                    });
                    continue;
                }

                // Container nodes (e.g. "Cover_Procedural") — recurse through.
                if (c.childCount > 0) CollectChildren(c);
            }
        }

        private bool ContainsTransform(Transform t)
        {
            for (int i = 0; i < _items.Count; i++)
                if (_items[i].t == t) return true;
            return false;
        }
    }
}
