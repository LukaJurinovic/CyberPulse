using UnityEngine;

namespace CyberPulse.Systems
{
    /// <summary>
    /// Renders 1000 tiny glowing cubes distributed around the arena using
    /// Graphics.DrawMeshInstanced — GPU-side, zero per-instance GameObjects.
    ///
    /// Each cube orbits a fixed cluster centre with a unique phase and radius.
    /// When the player enters _repelRadius units, nearby cubes scatter outward
    /// and slowly drift back once the player leaves.
    /// </summary>
    public class DataBitRenderer : MonoBehaviour
    {
        [SerializeField] private Transform _player;

        [Header("Count / Appearance")]
        [SerializeField] private int   _count     = 1000;
        [SerializeField] private float _cubeScale = 0.07f;
        [SerializeField] private Color _color     = new Color(0f, 2.4f, 2.5f, 1f); // HDR cyan

        [Header("Orbit")]
        [SerializeField] private float _orbitSpeedMin = 0.3f;
        [SerializeField] private float _orbitSpeedMax = 1.2f;
        [SerializeField] private float _orbitRadiusMin = 0.4f;
        [SerializeField] private float _orbitRadiusMax = 2.0f;

        [Header("Player Repulsion")]
        [SerializeField] private float _repelRadius   = 6f;
        [SerializeField] private float _repelForce    = 10f;
        [SerializeField] private float _repelReturn   = 4f;  // drift-back speed (units/sec)

        // Cluster centres — spread across the 60×60 arena
        private static readonly Vector3[] Centres =
        {
            new(-12f, 2f,  10f),   // near NW data node
            new( 12f, 2f,  10f),   // near NE data node
            new(  0f, 2f,  18f),   // near N data node
            new(  0f, 2f,  -5f),   // near S data node
            new(-20f, 3f,   0f),   // west mid
            new( 20f, 3f,   0f),   // east mid
            new( -9f, 2f,  -8f),   // SW cover
            new(  9f, 2f,  -8f),   // SE cover
            new(  0f, 4f,   8f),   // centre-north
            new(-22f, 5f,  15f),   // above jump platforms
            new(  0f, 2f, -18f),   // south open zone
            new( 14f, 3f,   0f),   // NE corridor
        };

        // Pre-allocated instance data (no per-frame GC)
        private Matrix4x4[] _matrices;
        private Vector3[]   _basePos;
        private float[]     _phase;
        private float[]     _speed;
        private float[]     _radius;
        private Vector3[]   _repelOffset;

        private Mesh     _mesh;
        private Material _material;

        // DrawMeshInstanced limit is 1023 per call — split into batches
        private const int BatchSize = 1023;
        private Matrix4x4[][] _batches;
        private int           _batchCount;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            BuildMesh();
            BuildMaterial();
            InitInstances();
            PrepareBatches();
        }

        private void Update()
        {
            float     time      = Time.time;
            float     dt        = Time.deltaTime;
            Vector3   playerPos = _player != null ? _player.position : new Vector3(0, -999, 0);
            var       scale     = Vector3.one * _cubeScale;

            for (int i = 0; i < _count; i++)
            {
                float angle = time * _speed[i] + _phase[i];
                var orbit = new Vector3(
                    Mathf.Cos(angle)                               * _radius[i],
                    Mathf.Sin(time * _speed[i] * 0.5f + _phase[i]) * 0.4f,
                    Mathf.Sin(angle)                               * _radius[i]);

                Vector3 worldPos = _basePos[i] + orbit;

                // Repulsion
                float dist = Vector3.Distance(worldPos, playerPos);
                if (dist < _repelRadius && dist > 0.01f)
                {
                    float strength = (1f - dist / _repelRadius) * _repelForce;
                    var   dir      = (worldPos - playerPos).normalized;
                    _repelOffset[i] = Vector3.Lerp(_repelOffset[i], dir * strength, dt * 8f);
                }
                else
                {
                    _repelOffset[i] = Vector3.Lerp(_repelOffset[i], Vector3.zero, dt * _repelReturn);
                }

                _matrices[i] = Matrix4x4.TRS(worldPos + _repelOffset[i], Quaternion.identity, scale);
            }

            // Copy into batches and draw
            for (int b = 0; b < _batchCount; b++)
            {
                int start = b * BatchSize;
                int size  = Mathf.Min(BatchSize, _count - start);
                System.Array.Copy(_matrices, start, _batches[b], 0, size);
                Graphics.DrawMeshInstanced(_mesh, 0, _material, _batches[b], size);
            }
        }

        // ── Setup helpers ─────────────────────────────────────────────────────

        private void BuildMesh()
        {
            // Borrow the shared cube mesh — no temp GameObject needed
            _mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            if (_mesh == null)
            {
                // Fallback: create a tiny mesh from a temporary primitive
                var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _mesh = tmp.GetComponent<MeshFilter>().sharedMesh;
                Destroy(tmp);
            }
        }

        private void BuildMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");

            _material                   = new Material(shader);
            _material.color             = _color;
            _material.enableInstancing  = true;
        }

        private void InitInstances()
        {
            _matrices    = new Matrix4x4[_count];
            _basePos     = new Vector3  [_count];
            _phase       = new float    [_count];
            _speed       = new float    [_count];
            _radius      = new float    [_count];
            _repelOffset = new Vector3  [_count];

            for (int i = 0; i < _count; i++)
            {
                var centre = Centres[i % Centres.Length];
                _basePos[i] = centre + new Vector3(
                    Random.Range(-4f, 4f),
                    Random.Range(-0.5f, 3f),
                    Random.Range(-4f, 4f));
                _phase[i]  = Random.Range(0f, Mathf.PI * 2f);
                _speed[i]  = Random.Range(_orbitSpeedMin, _orbitSpeedMax);
                _radius[i] = Random.Range(_orbitRadiusMin, _orbitRadiusMax);
            }
        }

        private void PrepareBatches()
        {
            _batchCount = Mathf.CeilToInt((float)_count / BatchSize);
            _batches    = new Matrix4x4[_batchCount][];
            for (int b = 0; b < _batchCount; b++)
            {
                int size    = Mathf.Min(BatchSize, _count - b * BatchSize);
                _batches[b] = new Matrix4x4[size];
            }
        }
    }
}
