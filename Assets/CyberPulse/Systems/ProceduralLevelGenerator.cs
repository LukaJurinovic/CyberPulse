using System.Collections.Generic;
using UnityEngine;

namespace CyberPulse.Systems
{
    public enum EnemyType { Seeker, SphereAerial, TriangleMirror, CylinderLauncher, CubeSplitter }

    public struct WaveDefinition
    {
        public float       SpawnTime;        // seconds into the song
        public EnemyType[] EnemyTypes;       // type per enemy slot
        public int         Count;
        public Vector3[]   SpawnPositions;   // world-space, y on floor
    }

    /// <summary>
    /// Translates a SongProfile into a WaveDefinition array.
    /// Static utility — no MonoBehaviour needed.
    /// </summary>
    public static class ProceduralLevelGenerator
    {
        // Eight evenly-spaced positions around the 60×60 arena perimeter.
        private static readonly Vector3[] SpawnRing =
        {
            new Vector3(-20f, 0f,  20f), new Vector3( 20f, 0f,  20f),
            new Vector3(-20f, 0f, -20f), new Vector3( 20f, 0f, -20f),
            new Vector3(  0f, 0f,  24f), new Vector3(  0f, 0f, -24f),
            new Vector3(-24f, 0f,   0f), new Vector3( 24f, 0f,   0f),
        };

        // ── Entry point ───────────────────────────────────────────────────────

        public static WaveDefinition[] Generate(SongProfile profile)
        {
            bool hasTimeline = profile.EnergyTimeline != null && profile.EnergyTimeline.Length > 0;
            if (!hasTimeline)
                return Fallback(profile.Duration);

            // Find high-energy peaks in the timeline to use as wave trigger times.
            var peakTimes = FindPeaks(profile.EnergyTimeline, profile.Duration);

            // Clamp target wave count: ~1 wave per 20s, 2-10 total.
            int maxWaves = Mathf.Clamp(Mathf.RoundToInt(profile.Duration / 20f), 2, 10);

            // When more peaks than slots: spread-select so waves are distributed across the
            // whole song rather than just front-loaded on the first N peaks.
            if (peakTimes.Count > maxWaves)
            {
                var spread = new List<float>(maxWaves);
                float step = (float)(peakTimes.Count - 1) / (maxWaves - 1);
                for (int i = 0; i < maxWaves; i++)
                    spread.Add(peakTimes[Mathf.RoundToInt(i * step)]);
                peakTimes = spread;
            }

            if (peakTimes.Count == 0)
                return Fallback(profile.Duration);

            // Gap-fill: insert a trickle wave at the midpoint of any gap > 25s, including
            // the stretch from the last wave to near the end of the song.
            const float maxGap = 25f;
            int gi = 1;
            while (gi < peakTimes.Count)
            {
                if (peakTimes[gi] - peakTimes[gi - 1] > maxGap)
                    peakTimes.Insert(gi, (peakTimes[gi - 1] + peakTimes[gi]) / 2f);
                else
                    gi++;
            }
            // Tail: keep appending waves every maxGap seconds until we're within 10s of the end.
            while (profile.Duration - peakTimes[peakTimes.Count - 1] > maxGap)
                peakTimes.Add(peakTimes[peakTimes.Count - 1] + maxGap);

            // Enemies per wave: 2-6 scaled by average energy.
            int baseCount = Mathf.Clamp(Mathf.RoundToInt(2f + profile.AverageEnergy * 6f), 2, 6);

            EnemyType[] availableTypes = AvailableTypes(profile.PeakEnergy);

            var waves = new WaveDefinition[peakTimes.Count];
            for (int i = 0; i < peakTimes.Count; i++)
            {
                int count = baseCount + (i % 3 == 2 ? 1 : 0);   // slight per-third escalation
                waves[i] = new WaveDefinition
                {
                    SpawnTime      = peakTimes[i],
                    Count          = count,
                    EnemyTypes     = PickTypes(availableTypes, count),
                    SpawnPositions = PickSpawnPositions(count),
                };
            }

            Debug.Log($"[ProceduralLevelGenerator] {waves.Length} waves generated " +
                      $"(BPM={profile.BPM:F0}, avg energy={profile.AverageEnergy:F2}, " +
                      $"first={peakTimes[0]:F0}s, last={peakTimes[peakTimes.Count-1]:F0}s).");
            return waves;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Finds local energy maxima that exceed 60% of peak energy, spaced at least 8s apart.
        /// Returns spawn times in seconds, guaranteed ≥ 5s into the song.
        /// </summary>
        private static List<float> FindPeaks(float[] timeline, float duration)
        {
            float tickInterval = duration / timeline.Length;
            float peakEnergy   = 0f;
            foreach (float e in timeline) if (e > peakEnergy) peakEnergy = e;
            float threshold = peakEnergy * 0.6f;

            var raw = new List<float>();
            for (int i = 1; i < timeline.Length - 1; i++)
            {
                if (timeline[i] >= threshold &&
                    timeline[i] >= timeline[i - 1] &&
                    timeline[i] >= timeline[i + 1])
                    raw.Add(i * tickInterval);
            }

            // Enforce minimum gap and ignore very early triggers.
            var filtered = new List<float>();
            float lastTime = -100f;
            foreach (float t in raw)
            {
                if (t >= 5f && t - lastTime >= 8f)
                {
                    filtered.Add(t);
                    lastTime = t;
                }
            }
            return filtered;
        }

        private static WaveDefinition[] Fallback(float duration)
        {
            int   count   = Mathf.Clamp(Mathf.RoundToInt(duration / 20f), 2, 6);
            float spacing = Mathf.Max(15f, duration / (count + 1));
            var   waves   = new WaveDefinition[count];
            for (int i = 0; i < count; i++)
                waves[i] = new WaveDefinition
                {
                    SpawnTime      = spacing * (i + 1),
                    Count          = 3 + (i % 2),
                    EnemyTypes     = new[] { EnemyType.Seeker },
                    SpawnPositions = PickSpawnPositions(3 + (i % 2)),
                };
            Debug.Log($"[ProceduralLevelGenerator] No timeline — fallback: {count} waves.");
            return waves;
        }

        private static EnemyType[] AvailableTypes(float peakEnergy)
        {
            // Progressive unlock by song intensity:
            //   0.0-0.3  → Seeker only (low-energy / chill tracks)
            //   0.3-0.5  → + Triangle Mirror Fighter
            //   0.5-0.7  → + Cylinder Homing Launcher
            //   0.7+     → + Sphere Aerial Striker + Cube Splitter
            var types = new List<EnemyType> { EnemyType.Seeker };
            if (peakEnergy >= 0.3f) types.Add(EnemyType.TriangleMirror);
            if (peakEnergy >= 0.5f) types.Add(EnemyType.CylinderLauncher);
            if (peakEnergy >= 0.7f) { types.Add(EnemyType.SphereAerial); types.Add(EnemyType.CubeSplitter); }
            return types.ToArray();
        }

        private static EnemyType[] PickTypes(EnemyType[] available, int count)
        {
            var types = new EnemyType[count];
            for (int i = 0; i < count; i++)
                types[i] = available[i % available.Length];
            return types;
        }

        private static Vector3[] PickSpawnPositions(int count)
        {
            var positions = new Vector3[count];
            for (int i = 0; i < count; i++)
                positions[i] = SpawnRing[i % SpawnRing.Length];
            return positions;
        }
    }
}
