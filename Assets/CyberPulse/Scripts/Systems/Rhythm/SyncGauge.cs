using System;
using UnityEngine;
using CyberPulse.Weapons;

namespace CyberPulse.Systems
{
    public class SyncGauge : MonoBehaviour
    {
        public static SyncGauge Instance { get; private set; }

        [Header("Rates")]
        [SerializeField] private float _offRhythmDrainPerSec = 1f;

        [Header("Debug")]
        [SerializeField] private bool _logChanges = false;

        public float Value      { get; private set; }
        public float Normalized => Value / 100f;

        public event Action<float> OnSyncChanged;
        public event Action        OnSyncFull;

        private bool _wasFull;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            if (BeatReactor.Instance != null)
                BeatReactor.Instance.OnOffRhythmTick += HandleOffRhythmTick;
        }

        private void OnDestroy()
        {
            if (BeatReactor.Instance != null)
                BeatReactor.Instance.OnOffRhythmTick -= HandleOffRhythmTick;
        }

        public void Add(float amount)
        {
            if (amount <= 0f) return;
            SetValue(Value + amount);
            if (_logChanges) Debug.Log($"[SyncGauge] +{amount:F0} → {Value:F0}");
        }

        public void Subtract(float amount)
        {
            if (amount <= 0f) return;
            SetValue(Value - amount);
        }

        /// <summary>
        /// Spends <paramref name="cost"/> SYNC and calls
        /// <paramref name="weapon"/>.TriggerSpecial() if successful.
        /// Returns true when the spend went through.
        /// </summary>
        public bool TrySpend(float cost, WeaponBase weapon)
        {
            if (Value < cost) return false;
            SetValue(Value - cost);
            weapon.TriggerSpecial();
            ScoreManager.Instance?.AddSpecialScore();
            if (_logChanges) Debug.Log($"[SyncGauge] Spent {cost:F0} on {weapon.WeaponName}");
            return true;
        }

        private void HandleOffRhythmTick(float dt)
        {
            Subtract(_offRhythmDrainPerSec * dt);
        }

        private void SetValue(float v)
        {
            float prev  = Value;
            Value       = Mathf.Clamp(v, 0f, 100f);
            if (Mathf.Approximately(prev, Value)) return;

            OnSyncChanged?.Invoke(Value);

            if (Value >= 100f && !_wasFull)
            {
                _wasFull = true;
                OnSyncFull?.Invoke();
            }
            else if (Value < 100f)
            {
                _wasFull = false;
            }
        }
    }
}
