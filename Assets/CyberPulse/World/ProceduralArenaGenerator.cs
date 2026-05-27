using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using CyberPulse.Systems;

namespace CyberPulse.World
{
    /// <summary>
    /// Generates all arena interior obstacles procedurally at runtime once song analysis completes.
    ///
    /// Five archetypes mixed based on EnergyVariance:
    ///   • Pillar   — tall thin column, creates chokepoints and line-of-sight breaks
    ///   • Wall     — long narrow slab, randomly oriented N-S or E-W
    ///   • Cover    — low crouchable box (occasionally elevated as a jump platform)
    ///   • Bunker   — wide medium block with a 35% chance of a perpendicular wing (L-shape)
    ///   • Platform — thin wide slab raised off the floor, replaces old hardcoded jump platforms
    ///
    /// Block count:  Lerp(8, 24, EnergyVariance)
    /// Block height: taller on high-energy tracks for each applicable archetype
    /// Same song always produces the same layout (RNG seeded from song parameters).
    /// Every block gets a NavMeshObstacle (carving) for runtime agent pathfinding.
    /// </summary>
    public class ProceduralArenaGenerator : MonoBehaviour
    {
        [Header("Layer")]
        [SerializeField] private int _groundLayerIndex;

        [Header("Block Count")]
        [SerializeField] private int _minBlocks = 8;
        [SerializeField] private int _maxBlocks = 24;

        [Header("Placement Constraints")]
        [SerializeField] private float _arenaHalf       = 24f;   // blocks stay within this XZ bound
        [SerializeField] private float _spawnClearance  = 9f;    // keep clear of player spawn (0,0,−22)
        [SerializeField] private float _centerClearance = 4f;    // keep centre fight-space open
        [SerializeField] private float _minSpacing      = 1.5f;  // min gap between adjacent blocks
        [SerializeField] private int   _maxAttempts     = 40;    // placement tries per block

        // Probability that a Wall or Bunker gets a perpendicular wing appended (L-shape).
        private const float WingChance = 0.35f;

        private static readonly Vector2 SpawnXZ = new Vector2(0f, -22f);
        private static readonly Color   BlockCol = new Color(0.08f, 0.06f, 0.15f);

        // Axis-aligned bounding rects of placed blocks (XZ plane, padded by _minSpacing).
        private readonly List<Rect> _occupied = new();

        private enum Archetype { Pillar, Wall, Cover, Bunker, Platform }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Start()
        {
            if (SongAnalyzer.Instance == null) { Generate(0.4f, 0.5f); return; }

            if (SongAnalyzer.Instance.IsAnalyzed)
                ApplyProfile(SongAnalyzer.Instance.Profile);
            else
                SongAnalyzer.Instance.OnAnalysisComplete += ApplyProfile;
        }

        private void OnDestroy()
        {
            if (SongAnalyzer.Instance != null)
                SongAnalyzer.Instance.OnAnalysisComplete -= ApplyProfile;
        }

        // ── Generation ────────────────────────────────────────────────────────

        private void ApplyProfile(SongProfile profile) =>
            Generate(profile.EnergyVariance, profile.AverageEnergy);

        private void Generate(float variance, float avgEnergy)
        {
            _occupied.Clear();

            int blockCount = Mathf.RoundToInt(Mathf.Lerp(_minBlocks, _maxBlocks, variance));
            int placed     = 0;

            Random.State prevState = Random.state;
            Random.InitState(Mathf.RoundToInt(variance * 1000f + avgEnergy * 999f));

            var root = new GameObject("Cover_Procedural");
            root.transform.SetParent(transform, false);

            for (int i = 0; i < blockCount; i++)
                if (TryPlaceBlock(root, variance, avgEnergy)) placed++;

            Random.state = prevState;

            Debug.Log($"[ProceduralArenaGenerator] Placed {placed}/{blockCount} blocks " +
                      $"(variance={variance:F2}, energy={avgEnergy:F2}).");
        }

        // ── Block placement ───────────────────────────────────────────────────

        private bool TryPlaceBlock(GameObject parent, float variance, float avgEnergy)
        {
            Archetype arch = PickArchetype(variance);

            for (int attempt = 0; attempt < _maxAttempts; attempt++)
            {
                GetDimensions(arch, avgEnergy, out float w, out float d, out float h);

                // Walls get randomly oriented N-S or E-W.
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

                // Elevation: Platforms always raised; Cover occasionally raised as a jump pad.
                float yBase = arch == Archetype.Platform
                    ? Random.Range(0.8f, 5.5f)
                    : (arch == Archetype.Cover && Random.value < 0.20f)
                        ? Random.Range(0.8f, 2.5f)
                        : 0f;

                PlaceBlock(parent, x, yBase + h * 0.5f, z, w, h, d);
                _occupied.Add(new Rect(x - w * 0.5f, z - d * 0.5f, w, d));

                // L-shape wing for Wall / Bunker.
                if ((arch == Archetype.Wall || arch == Archetype.Bunker)
                    && Random.value < WingChance)
                {
                    TryPlaceWing(parent, x, z, w, d, h);
                }

                return true;
            }
            return false;
        }

        /// <summary>
        /// Tries to attach a perpendicular slab to one end of the placed block, forming an L.
        /// The wing is thin in X and extends along Z; it is offset in Z so it doesn't overlap
        /// the parent, producing a genuine L rather than a T.
        /// </summary>
        private void TryPlaceWing(GameObject parent,
            float bx, float bz, float bw, float bd, float bh)
        {
            float wingW = Random.Range(0.4f, 1.2f);
            float wingD = Random.Range(2f, 5.5f);
            float wingH = bh * Random.Range(0.5f, 1.0f);

            // Attach to the left or right X-end of the parent block.
            bool  right = Random.value > 0.5f;
            float wx    = right
                ? bx + bw * 0.5f + wingW * 0.5f
                : bx - bw * 0.5f - wingW * 0.5f;

            // Offset in Z so the wing sits at one end, not centred → true L-shape.
            float zSign = Random.value > 0.5f ? 1f : -1f;
            float wz    = bz + zSign * (bd * 0.5f + wingD * 0.5f);

            // Boundary check.
            if (Mathf.Abs(wx) + wingW * 0.5f > _arenaHalf) return;
            if (Mathf.Abs(wz) + wingD * 0.5f > _arenaHalf) return;

            var wingXZ = new Vector2(wx, wz);
            if (Vector2.Distance(wingXZ, SpawnXZ) < _spawnClearance) return;
            if (wingXZ.magnitude                  < _centerClearance) return;

            var candidate = new Rect(wx - wingW * 0.5f - _minSpacing, wz - wingD * 0.5f - _minSpacing,
                                     wingW + _minSpacing * 2f, wingD + _minSpacing * 2f);
            foreach (var r in _occupied)
                if (r.Overlaps(candidate)) return;

            PlaceBlock(parent, wx, wingH * 0.5f, wz, wingW, wingH, wingD);
            _occupied.Add(new Rect(wx - wingW * 0.5f, wz - wingD * 0.5f, wingW, wingD));
        }

        // ── Archetype helpers ─────────────────────────────────────────────────

        private Archetype PickArchetype(float variance)
        {
            // High variance → more disruptive geometry (pillars, walls).
            // Low variance  → more open, flat cover.
            float r           = Random.value;
            float pillarCut   = 0.08f + variance * 0.10f;
            float wallCut     = pillarCut  + 0.20f + variance * 0.12f;
            float platformCut = wallCut    + 0.15f;
            float coverCut    = platformCut + 0.30f;
            // remainder → Bunker

            if (r < pillarCut)   return Archetype.Pillar;
            if (r < wallCut)     return Archetype.Wall;
            if (r < platformCut) return Archetype.Platform;
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
                    w = Random.Range(2.0f,  5.5f);
                    d = Random.Range(2.0f,  4.5f);
                    h = Random.Range(0.2f,  0.45f);   // thin slab — stand on top
                    break;

                case Archetype.Cover:
                    w = Random.Range(1.5f,  4.0f);
                    d = Random.Range(1.5f,  3.5f);
                    h = Random.Range(0.7f,  1.8f);    // crouchable / vaultable height
                    break;

                default: // Bunker
                    w = Random.Range(2.5f,  5.5f);
                    d = Random.Range(2.0f,  4.5f);
                    h = Mathf.Lerp(1.5f, 2.8f, avgEnergy);
                    break;
            }
        }

        // ── GameObject creation ───────────────────────────────────────────────

        private void PlaceBlock(GameObject parent,
            float x, float y, float z, float w, float h, float d)
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
            obs.carving             = true;
            obs.carveOnlyStationary = true;
            obs.size                = Vector3.one;   // matches transform.localScale in world space
            obs.center              = Vector3.zero;
        }
    }
}
