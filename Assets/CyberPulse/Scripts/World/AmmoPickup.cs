using UnityEngine;
using CyberPulse.Weapons;

namespace CyberPulse.World
{
    /// <summary>
    /// A walk-over ammo pickup. Dropped by enemies on death (see EnemyHealth) and
    /// built entirely at runtime via <see cref="Spawn"/> so no prefab asset is needed.
    /// On contact with the player it tops up the active weapon's reserve ammo.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class AmmoPickup : MonoBehaviour
    {
        [SerializeField] private int   _ammoAmount = 12;
        [SerializeField] private float _lifetime   = 12f;   // despawn if never collected
        [SerializeField] private float _spinSpeed  = 120f;  // degrees/sec
        [SerializeField] private float _bobHeight  = 0.18f;
        [SerializeField] private float _bobSpeed   = 3f;

        private Transform _visual;
        private float     _baseY;
        private float     _phase;

        private static Material _sharedMat;

        /// <summary>
        /// Build a pickup at a world position and return it. Constructs its own visual,
        /// trigger collider, and emissive material so callers need no prefab reference.
        /// </summary>
        public static AmmoPickup Spawn(Vector3 position, int amount)
        {
            var go = new GameObject("AmmoPickup");
            go.transform.position = position + Vector3.up * 0.6f;

            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            visual.transform.SetParent(go.transform, false);
            visual.transform.localScale    = Vector3.one * 0.45f;
            visual.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            Destroy(visual.GetComponent<Collider>());
            visual.GetComponent<MeshRenderer>().sharedMaterial = PickupMaterial();

            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius    = 1.2f;

            var pickup = go.AddComponent<AmmoPickup>();
            pickup._ammoAmount = amount;
            return pickup;
        }

        private void Start()
        {
            _visual = transform.childCount > 0 ? transform.GetChild(0) : null;
            _baseY  = transform.position.y;
            _phase  = Random.value * Mathf.PI * 2f;   // desync simultaneous drops
            if (_lifetime > 0f) Destroy(gameObject, _lifetime);
        }

        private void Update()
        {
            // Spin the gem and bob it so it reads as a collectible against the floor.
            if (_visual != null)
                _visual.Rotate(0f, _spinSpeed * Time.deltaTime, 0f, Space.World);

            float y = _baseY + Mathf.Sin(Time.time * _bobSpeed + _phase) * _bobHeight;
            var p = transform.position;
            transform.position = new Vector3(p.x, y, p.z);
        }

        private void OnTriggerEnter(Collider other)
        {
            // The player's collider may sit on a child; search upward for the holder.
            var holder = other.GetComponentInParent<WeaponHolder>();
            if (holder == null) return;

            holder.ActiveWeapon?.AddReserveAmmo(_ammoAmount);
            Destroy(gameObject);
        }

        private static Material PickupMaterial()
        {
            if (_sharedMat != null) return _sharedMat;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            _sharedMat = new Material(shader) { name = "M_AmmoPickup" };
            _sharedMat.SetColor("_BaseColor",     new Color(0.1f, 1f, 0.6f));
            _sharedMat.SetColor("_Color",         new Color(0.1f, 1f, 0.6f)); // Standard fallback
            _sharedMat.SetColor("_EmissionColor", new Color(0.2f, 2.4f, 1.3f)); // HDR for Bloom
            _sharedMat.EnableKeyword("_EMISSION");
            return _sharedMat;
        }
    }
}
