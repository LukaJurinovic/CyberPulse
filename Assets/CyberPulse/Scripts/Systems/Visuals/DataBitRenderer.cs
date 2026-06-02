using System.Collections.Generic;
using UnityEngine;
using CyberPulse.World;

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

        [Header("Distribution")]
        [Range(0f, 1f)]
        [SerializeField] private float _elevatedFraction = 0.6f;  // share of bits clustered around elevated anchors
        [SerializeField] private float _elevatedJitter   = 1.6f;  // XZ spread around each elevated anchor (metres)
        [SerializeField] private float _elevatedHeight   = 1.4f;  // extra height above anchor

        // Default ground-level cluster centres — used when no elevated anchors exist
        // (e.g. before ProceduralArenaGenerator has run).
        private static readonly Vector3[] GroundCentres =
        {
            new(-12f, 1.2f,  10f),
            new( 12f, 1.2f,  10f),
            new(  0f, 1.2f,  18f),
            new(  0f, 1.2f,  -5f),
            new(-20f, 1.2f,   0f),
            new( 20f, 1.2f,   0f),
            new( -9f, 1.2f,  -8f),
            new(  9f, 1.2f,  -8f),
            new(  0f, 1.2f,   8f),
            new(  0f, 1.2f, -18f),
            new( 14f, 1.2f,   0f),
            new(-14f, 1.2f, -14f),
        };

        private bool _layoutDirty = true;

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

        private void OnEnable()
        {
            // Re-layout once the arena generator publishes elevated anchors.
            if (ProceduralArenaGenerator.Instance != null)
                ProceduralArenaGenerator.Instance.OnArenaGenerated += OnArenaGenerated;
        }

        private void OnDisable()
        {
            if (ProceduralArenaGenerator.Instance != null)
                ProceduralArenaGenerator.Instance.OnArenaGenerated -= OnArenaGenerated;
        }

        private void OnArenaGenerated() => _layoutDirty = true;

        private void Update()
        {
            // If the arena generator has finished after our Awake, restamp
            // base positions to cluster bits around elevated anchors.
            if (_layoutDirty)
            {
                RebuildBasePositions();
                _layoutDirty = false;
            }

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
                _phase[i]  = Random.Range(0f, Mathf.PI * 2f);
                _speed[i]  = Random.Range(_orbitSpeedMin, _orbitSpeedMax);
                _radius[i] = Random.Range(_orbitRadiusMin, _orbitRadiusMax);
            }
            RebuildBasePositions();
        }

        /// <summary>
        /// Recompute each bit's home position. The first <c>_elevatedFraction</c>
        /// of bits cluster tightly around platform/tower tops (ProceduralArenaGenerator
        /// anchors); the remainder spread across ground-level centres. Falls back to
        /// ground-only when no anchors are available yet.
        /// </summary>
        private void RebuildBasePositions()
        {
            IReadOnlyList<Vector3> elevated = null;
            if (ProceduralArenaGenerator.Instance != null
                && ProceduralArenaGenerator.Instance.ElevatedAnchors.Count > 0)
            {
                elevated = ProceduralArenaGenerator.Instance.ElevatedAnchors;
            }

            int elevatedCount = elevated != null
                ? Mathf.RoundToInt(_count * _elevatedFraction)
                : 0;

            for (int i = 0; i < _count; i++)
            {
                if (i < elevatedCount)
                {
                    var anchor = elevated[i % elevated.Count];
                    _basePos[i] = anchor + new Vector3(
                        Random.Range(-_elevatedJitter, _elevatedJitter),
                        Random.Range(0.1f, _elevatedHeight),
                        Random.Range(-_elevatedJitter, _elevatedJitter));
                }
                else
                {
                    var centre = GroundCentres[i % GroundCentres.Length];
                    _basePos[i] = centre + new Vector3(
                        Random.Range(-4f, 4f),
                        Random.Range(-0.3f, 1.8f),
                        Random.Range(-4f, 4f));
                }
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
