using UnityEngine;

namespace CyberPulse.Enemy
{
    /// <summary>
    /// Ground-ring warning indicator. Fades from cyan to red over its lifetime,
    /// then self-destructs. Used by EnemySphereAerial for the charge telegraph.
    /// </summary>
    public class AoEIndicator : MonoBehaviour
    {
        private LineRenderer _ring;
        private float        _duration;
        private float        _elapsed;

        private static readonly Color CyanColor = new Color(0f, 0.96f, 1f);
        private static readonly Color RedColor  = new Color(1f, 0.15f, 0.05f);

        public static AoEIndicator Create(Vector3 groundCenter, float radius, float duration)
        {
            var go  = new GameObject("AoEIndicator");
            var ind = go.AddComponent<AoEIndicator>();
            ind.Setup(groundCenter, radius, duration);
            return ind;
        }

        private void Setup(Vector3 center, float radius, float duration)
        {
            transform.position = center + Vector3.up * 0.05f;
            _duration          = duration;

            _ring = gameObject.AddComponent<LineRenderer>();
            _ring.useWorldSpace   = false;
            _ring.loop            = true;
            _ring.widthMultiplier = 0.12f;
            _ring.positionCount   = 40;
            _ring.material        = new Material(
                Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));

            for (int i = 0; i < 40; i++)
            {
                float a = i / 40f * Mathf.PI * 2f;
                _ring.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
            }
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _duration);

            Color col = Color.Lerp(CyanColor, RedColor, t);
            _ring.startColor     = col;
            _ring.endColor       = col;
            _ring.material.color = col;

            if (_elapsed >= _duration)
                Destroy(gameObject);
        }
    }
}
