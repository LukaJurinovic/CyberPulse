using System.Collections;
using UnityEngine;
using CyberPulse.Combat;

namespace CyberPulse.Weapons
{
    /// <summary>
    /// Hitscan weapon — instant raycast hit, optional pellet spread (shotgun mode).
    /// Draws a short procedural LineRenderer bullet trace that fades out.
    /// </summary>
    public class HitscanWeapon : WeaponBase
    {
        [Header("Hitscan")]
        [SerializeField] private int _pelletsPerShot = 1;
        [SerializeField] private float _spreadAngle = 0f;
        [SerializeField] private int _damage = 25;
        [SerializeField] private float _range = 200f;
        [SerializeField] private LayerMask _hitMask = ~0;

        [Header("Bullet Trace")]
        [SerializeField] private float _traceDuration = 0.06f;
        [SerializeField] private float _traceWidth = 0.02f;

        protected override void FireProjectile(Transform cameraTransform)
        {
            for (int i = 0; i < _pelletsPerShot; i++)
            {
                Vector3 dir = cameraTransform.forward;

                if (_spreadAngle > 0f)
                {
                    dir = Quaternion.Euler(
                        Random.Range(-_spreadAngle, _spreadAngle),
                        Random.Range(-_spreadAngle, _spreadAngle),
                        0f) * dir;
                }

                Ray ray = new Ray(cameraTransform.position, dir);
                Vector3 endpoint = cameraTransform.position + dir * _range;

                if (Physics.Raycast(ray, out RaycastHit hit, _range, _hitMask, QueryTriggerInteraction.Ignore))
                {
                    endpoint = hit.point;
                    var damageable = hit.collider.GetComponentInParent<IDamageable>();
                    damageable?.TakeDamage(_damage);
                }

                StartCoroutine(DrawTrace(cameraTransform.position, endpoint));
            }
        }

        private IEnumerator DrawTrace(Vector3 start, Vector3 end)
        {
            GameObject traceObj = new GameObject("BulletTrace");
            LineRenderer lr = traceObj.AddComponent<LineRenderer>();

            lr.material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            lr.startColor = lr.endColor = new Color(1f, 0.9f, 0.5f, 0.8f);
            lr.startWidth = lr.endWidth = _traceWidth;
            lr.SetPosition(0, start);
            lr.SetPosition(1, end);
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.useWorldSpace = true;

            float elapsed = 0f;
            Color startColor = lr.startColor;

            while (elapsed < _traceDuration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(startColor.a, 0f, elapsed / _traceDuration);
                Color c = new Color(startColor.r, startColor.g, startColor.b, alpha);
                lr.startColor = lr.endColor = c;
                yield return null;
            }

            Destroy(traceObj);
        }
    }
}
