using System.Collections;
using UnityEngine;

namespace CyberPulse.Enemy
{
    public class EnemyDeathShards : MonoBehaviour
    {
        [Header("Shards")]
        [SerializeField] private int   _shardCount      = 14;
        [SerializeField] private float _shardMinSize    = 0.08f;
        [SerializeField] private float _shardMaxSize    = 0.28f;
        [SerializeField] private float _explosionForce  = 420f;
        [SerializeField] private float _explosionRadius = 2.5f;
        [SerializeField] private float _upwardBias      = 0.6f;
        [SerializeField] private float _fadeDuration    = 2.0f;
        [SerializeField] private Color _shardColor      = new Color(1f, 0.25f, 0.05f);
        [SerializeField] private Color _shardEmissive   = new Color(2f, 0.4f,  0.1f);

        [SerializeField] private Renderer _enemyRenderer;

        public void SetRenderer(Renderer r) { _enemyRenderer = r; }

        public void Explode()
        {
            if (_enemyRenderer != null)
                _enemyRenderer.enabled = false;

            Vector3 origin = transform.position + Vector3.up;

            for (int i = 0; i < _shardCount; i++)
            {
                float   size  = Random.Range(_shardMinSize, _shardMaxSize);
                Vector3 offset = Random.insideUnitSphere * 0.5f;
                var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);

                Destroy(shard.GetComponent<BoxCollider>());

                shard.transform.position = origin + offset;
                shard.transform.rotation = Random.rotation;
                shard.transform.localScale = Vector3.one * size;

                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                mat.SetColor("_BaseColor",     _shardColor);
                mat.SetColor("_EmissionColor", _shardEmissive);
                mat.EnableKeyword("_EMISSION");
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend",   0f);
                mat.SetFloat("_AlphaClip", 0f);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                shard.GetComponent<Renderer>().material = mat;

                var rb = shard.AddComponent<Rigidbody>();
                rb.mass = 0.1f;
                rb.AddExplosionForce(_explosionForce, origin, _explosionRadius, _upwardBias, ForceMode.Impulse);

                StartCoroutine(FadeShard(shard, mat, _fadeDuration));
            }
        }

        private static IEnumerator FadeShard(GameObject shard, Material mat, float duration)
        {
            float elapsed = 0f;
            Color startColor = mat.GetColor("_BaseColor");

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = 1f - Mathf.Clamp01(elapsed / duration);
                Color c = startColor;
                c.a = alpha;
                mat.SetColor("_BaseColor", c);
                yield return null;
            }

            Destroy(shard);
        }
    }
}
