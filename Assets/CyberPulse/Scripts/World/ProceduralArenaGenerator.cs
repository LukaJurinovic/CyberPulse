using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using CyberPulse.Systems;
using Random = UnityEngine.Random;

namespace CyberPulse.World
{
    public class ProceduralArenaGenerator : MonoBehaviour
    {
        public static ProceduralArenaGenerator Instance { get; private set; }

        [Header("Layer")]
        [SerializeField] private int _groundLayerIndex;

        [Header("Stacked layer placement")]
        [Tooltip("World Y of this layer's floor. All blocks/pads are offset up by this.")]
        [SerializeField] private float _floorY;
        [Tooltip("Per-layer seed offset so stacked floors get distinct layouts.")]
        [SerializeField] private int _seedOffset;

        [Header("Block Count")]
        [SerializeField] private int _minBlocks = 10;
        [SerializeField] private int _maxBlocks = 28;

        [Header("Placement Constraints")]
        [SerializeField] private float _arenaHalf       = 24f;
        [SerializeField] private float _spawnClearance  = 9f;
        [SerializeField] private float _centerClearance = 4f;
        [SerializeField] private float _minSpacing      = 1.5f;
        [SerializeField] private int   _maxAttempts     = 40;

        [Header("Jump Pad")]
        [SerializeField] private LayerMask _playerLayer;

        private const float WingChance = 0.35f;

        private static readonly Vector2 SpawnXZ  = new Vector2(0f, -22f);
        private static readonly Color   BlockCol = new Color(0.08f, 0.06f, 0.15f);

        private readonly List<Rect>     _occupied = new();
        private readonly List<Vector3>  _elevatedAnchors = new();

        public IReadOnlyList<Vector3> ElevatedAnchors => _elevatedAnchors;

        public event Action OnArenaGenerated;

        public bool HasGenerated { get; private set; }

        private enum Archetype { Pillar, Wall, Cover, Bunker, Platform, Tower }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (SongAnalyzer.Instance != null)
                SongAnalyzer.Instance.OnAnalysisComplete -= ApplyProfile;
        }

        private void Start()
        {
            if (SongAnalyzer.Instance == null) { Generate(0.4f, 0.5f); return; }

            if (SongAnalyzer.Instance.IsAnalyzed)
                ApplyProfile(SongAnalyzer.Instance.Profile);
            else
                SongAnalyzer.Instance.OnAnalysisComplete += ApplyProfile;
        }

        private void ApplyProfile(SongProfile profile) =>
            Generate(profile.EnergyVariance, profile.AverageEnergy);

        private void Generate(float variance, float avgEnergy)
        {
            _occupied.Clear();
            _elevatedAnchors.Clear();

            int blockCount = Mathf.RoundToInt(Mathf.Lerp(_minBlocks, _maxBlocks, variance));
            int placed     = 0;

            Random.State prevState = Random.state;
            Random.InitState(Mathf.RoundToInt(variance * 1000f + avgEnergy * 999f) + _seedOffset * 7919);

            var root = new GameObject("Cover_Procedural");
            root.transform.SetParent(transform, false);

            for (int i = 0; i < blockCount; i++)
                if (TryPlaceBlock(root, variance, avgEnergy)) placed++;

            Random.state = prevState;
            HasGenerated = true;

            Debug.Log($"[ProceduralArenaGenerator] Placed {placed}/{blockCount} blocks " +
                      $"({_elevatedAnchors.Count} elevated, variance={variance:F2}, energy={avgEnergy:F2}, floorY={_floorY:F1}).");

            OnArenaGenerated?.Invoke();
        }

        private bool TryPlaceBlock(GameObject parent, float variance, float avgEnergy)
        {
            Archetype arch = PickArchetype(variance);

            for (int attempt = 0; attempt < _maxAttempts; attempt++)
            {
                GetDimensions(arch, avgEnergy, out float w, out float d, out float h);

                if (arch == Archetype.Wall && Random.value > 0.5f)
                    (w, d) = (d, w);

                float x  = Random.Range(-_arenaHalf + w * 0.5f, _arenaHalf - w * 0.5f);
                float z  = Random.Range(-_arenaHalf + d * 0.5f, _arenaHalf - d * 0.5f);
                var   xz = new Vector2(x, z);

                if (Vector2.Distance(xz, SpawnXZ) < _spawnClearance) continue;
                if (xz.magnitude                  < _centerClearance) continue;

                var candidate = new Rect(x - w * 0.5f - _minSpacing, z - d * 0.5f - _minSpacing,
                                         w + _minSpacing * 2f, d + _minSpacing * 2f);
                bool overlap = false;
                foreach (var r in _occupied)
                    if (r.Overlaps(candidate)) { overlap = true; break; }
                if (overlap) continue;

                float yBase = arch switch
                {
                    Archetype.Tower    => Random.Range(4.0f, 6.0f),
                    Archetype.Platform => Random.Range(2.5f, 4.5f),
                    Archetype.Cover    => Random.value < 0.25f ? Random.Range(0.8f, 2.0f) : 0f,
                    _                  => 0f,
                };

                bool carveNavMesh = yBase < 0.5f;
                PlaceBlock(parent, x, _floorY + yBase + h * 0.5f, z, w, h, d, carveNavMesh);
                _occupied.Add(new Rect(x - w * 0.5f, z - d * 0.5f, w, d));

                if (arch == Archetype.Tower || arch == Archetype.Platform)
                {
                    float topY = _floorY + yBase + h + 0.2f;
                    _elevatedAnchors.Add(new Vector3(x, topY, z));
                    TryPlaceJumpPad(parent, x, z, w, d);
                }

                if ((arch == Archetype.Wall || arch == Archetype.Bunker)
                    && Random.value < WingChance)
                {
                    TryPlaceWing(parent, x, z, w, d, h);
                }

                return true;
            }
            return false;
        }

        private void TryPlaceWing(GameObject parent,
            float bx, float bz, float bw, float bd, float bh)
        {
            float wingW = Random.Range(0.4f, 1.2f);
            float wingD = Random.Range(2f, 5.5f);
            float wingH = bh * Random.Range(0.5f, 1.0f);

            bool  right = Random.value > 0.5f;
            float wx    = right
                ? bx + bw * 0.5f + wingW * 0.5f
                : bx - bw * 0.5f - wingW * 0.5f;

            float zSign = Random.value > 0.5f ? 1f : -1f;
            float wz    = bz + zSign * (bd * 0.5f + wingD * 0.5f);

            if (Mathf.Abs(wx) + wingW * 0.5f > _arenaHalf) return;
            if (Mathf.Abs(wz) + wingD * 0.5f > _arenaHalf) return;

            var wingXZ = new Vector2(wx, wz);
            if (Vector2.Distance(wingXZ, SpawnXZ) < _spawnClearance) return;
            if (wingXZ.magnitude                  < _centerClearance) return;

            var candidate = new Rect(wx - wingW * 0.5f - _minSpacing, wz - wingD * 0.5f - _minSpacing,
                                     wingW + _minSpacing * 2f, wingD + _minSpacing * 2f);
            foreach (var r in _occupied)
                if (r.Overlaps(candidate)) return;

            PlaceBlock(parent, wx, _floorY + wingH * 0.5f, wz, wingW, wingH, wingD, carveNavMesh: true);
            _occupied.Add(new Rect(wx - wingW * 0.5f, wz - wingD * 0.5f, wingW, wingD));
        }

        private void TryPlaceJumpPad(GameObject parent, float bx, float bz, float bw, float bd)
        {
            const float padSize = 1.4f;
            const float padGap  = 1.2f;

            Vector2[] offsets =
            {
                new Vector2( bw * 0.5f + padGap,                  0f),
                new Vector2(-bw * 0.5f - padGap,                  0f),
                new Vector2(                    0f,  bd * 0.5f + padGap),
                new Vector2(                    0f, -bd * 0.5f - padGap),
            };

            for (int i = 0; i < offsets.Length; i++)
            {
                float px = bx + offsets[i].x;
                float pz = bz + offsets[i].y;

                if (Mathf.Abs(px) > _arenaHalf - padSize * 0.5f) continue;
                if (Mathf.Abs(pz) > _arenaHalf - padSize * 0.5f) continue;
                if (Vector2.Distance(new Vector2(px, pz), SpawnXZ) < _spawnClearance) continue;

                var candidate = new Rect(px - padSize * 0.5f, pz - padSize * 0.5f,
                                         padSize, padSize);
                bool blocked = false;
                foreach (var r in _occupied)
                    if (r.Overlaps(candidate)) { blocked = true; break; }
                if (blocked) continue;

                PlaceJumpPad(parent, px, pz, padSize);
                _occupied.Add(new Rect(px - padSize * 0.5f, pz - padSize * 0.5f, padSize, padSize));
                return;
            }
        }

        private Archetype PickArchetype(float variance)
        {
            float r           = Random.value;
            float pillarCut   = 0.08f + variance * 0.08f;
            float wallCut     = pillarCut  + 0.18f + variance * 0.10f;
            float platformCut = wallCut    + 0.18f;
            float towerCut    = platformCut + 0.12f + variance * 0.05f;
            float coverCut    = towerCut    + 0.25f;

            if (r < pillarCut)   return Archetype.Pillar;
            if (r < wallCut)     return Archetype.Wall;
            if (r < platformCut) return Archetype.Platform;
            if (r < towerCut)    return Archetype.Tower;
            if (r < coverCut)    return Archetype.Cover;
            return Archetype.Bunker;
        }

        private static void GetDimensions(Archetype arch, float avgEnergy,
            out float w, out float d, out float h)
        {
            switch (arch)
            {
                case Archetype.Pillar:
                    w = Random.Range(0.5f,  1.4f);
                    d = Random.Range(0.5f,  1.4f);
                    h = Mathf.Lerp(2.5f, 6.0f, avgEnergy);
                    break;

                case Archetype.Wall:
                    w = Random.Range(3.5f,  8.0f);
                    d = Random.Range(0.4f,  1.0f);
                    h = Mathf.Lerp(1.5f, 3.5f, avgEnergy);
                    break;

                case Archetype.Platform:
                    w = Random.Range(2.5f,  5.5f);
                    d = Random.Range(2.5f,  4.5f);
                    h = Random.Range(0.25f, 0.45f);
                    break;

                case Archetype.Tower:
                    w = Random.Range(2.0f,  3.5f);
                    d = Random.Range(2.0f,  3.5f);
                    h = Random.Range(0.4f,  0.6f);
                    break;

                case Archetype.Cover:
                    w = Random.Range(1.5f,  4.0f);
                    d = Random.Range(1.5f,  3.5f);
                    h = Random.Range(0.7f,  1.8f);
                    break;

                default:
                    w = Random.Range(2.5f,  5.5f);
                    d = Random.Range(2.0f,  4.5f);
                    h = Mathf.Lerp(1.5f, 2.8f, avgEnergy);
                    break;
            }
        }

        private void PlaceBlock(GameObject parent,
            float x, float y, float z, float w, float h, float d, bool carveNavMesh)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name  = "Cover";
            go.layer = _groundLayerIndex;
            go.transform.SetParent(parent.transform, false);
            go.transform.position   = new Vector3(x, y, z);
            go.transform.localScale = new Vector3(w, h, d);

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                                ?? Shader.Find("Standard"));
            mat.SetColor("_BaseColor", BlockCol);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;

            var obs = go.AddComponent<NavMeshObstacle>();
            obs.carving             = carveNavMesh;
            obs.carveOnlyStationary = true;
            obs.size                = Vector3.one;
            obs.center              = Vector3.zero;
        }

        private void PlaceJumpPad(GameObject parent, float x, float z, float size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name  = "JumpPad";
            go.layer = _groundLayerIndex;
            go.transform.SetParent(parent.transform, false);
            go.transform.position   = new Vector3(x, _floorY + 0.06f, z);
            go.transform.localScale = new Vector3(size, 0.12f, size);

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                                ?? Shader.Find("Standard"));
            mat.SetColor("_BaseColor",     new Color(0.05f, 0.6f, 0.9f));
            mat.SetColor("_EmissionColor", new Color(0f,    2.0f, 3.0f));
            mat.EnableKeyword("_EMISSION");
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;

            Destroy(go.GetComponent<BoxCollider>());

            var trigger = new GameObject("Trigger");
            trigger.transform.SetParent(go.transform, false);
            trigger.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            var col = trigger.AddComponent<BoxCollider>();
            col.size      = new Vector3(1.05f, 2.4f, 1.05f);
            col.isTrigger = true;

            var pad = trigger.AddComponent<JumpPad>();
            pad.SetPlayerLayer(_playerLayer);
        }
    }
}
