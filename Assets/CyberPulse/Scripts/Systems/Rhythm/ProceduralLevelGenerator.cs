using System.Collections.Generic;
using UnityEngine;

namespace CyberPulse.Systems
{
    public enum EnemyType { Seeker, SphereAerial, TriangleMirror, CylinderLauncher, CubeSplitter }

    public struct WaveDefinition
    {
        public float       SpawnTime;
        public EnemyType[] EnemyTypes;
        public int         Count;
        public Vector3[]   SpawnPositions;
    }

    /// <summary>
    /// Translates a SongProfile into a WaveDefinition array.
    /// Static utility — no MonoBehaviour needed.
    /// </summary>
    public static class ProceduralLevelGenerator
    {

        public static WaveDefinition[] Generate(SongProfile profile)
        {
            bool hasTimeline = profile.EnergyTimeline != null && profile.EnergyTimeline.Length > 0;
            if (!hasTimeline)
                return Fallback(profile.Duration);

            var peakTimes = FindPeaks(profile.EnergyTimeline, profile.Duration);

            int maxWaves = Mathf.Clamp(Mathf.RoundToInt(profile.Duration / 20f), 2, 10);

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

            const float maxGap = 25f;
            int gi = 1;
            while (gi < peakTimes.Count)
            {
                if (peakTimes[gi] - peakTimes[gi - 1] > maxGap)
                    peakTimes.Insert(gi, (peakTimes[gi - 1] + peakTimes[gi]) / 2f);
                else
                    gi++;
            }
            while (profile.Duration - peakTimes[peakTimes.Count - 1] > maxGap)
                peakTimes.Add(peakTimes[peakTimes.Count - 1] + maxGap);

            int baseCount = Mathf.Clamp(Mathf.RoundToInt(3f + profile.AverageEnergy * 6f), 3, 8);

            var waves = new WaveDefinition[peakTimes.Count];
            for (int i = 0; i < peakTimes.Count; i++)
            {
                float progress    = profile.Duration > 0f ? peakTimes[i] / profile.Duration : 0f;
                int   count       = Mathf.Clamp(baseCount + Mathf.RoundToInt(progress * 4f), 3, 12);
                EnemyType[] avail = AvailableTypes(progress);
                waves[i] = new WaveDefinition
                {
                    SpawnTime      = peakTimes[i],
                    Count          = count,
                    EnemyTypes     = PickTypes(avail, count),
                    SpawnPositions = PickSpawnPositions(count),
                };
            }

            Debug.Log($"[ProceduralLevelGenerator] {waves.Length} waves generated " +
                      $"(BPM={profile.BPM:F0}, avg energy={profile.AverageEnergy:F2}, " +
                      $"first={peakTimes[0]:F0}s, last={peakTimes[peakTimes.Count-1]:F0}s).");
            return waves;
        }

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
            {
                float t        = spacing * (i + 1);
                float progress = duration > 0f ? t / duration : 0f;
                int   cnt      = 4 + (i % 3);
                waves[i] = new WaveDefinition
                {
                    SpawnTime      = t,
                    Count          = cnt,
                    EnemyTypes     = PickTypes(AvailableTypes(progress), cnt),
                    SpawnPositions = PickSpawnPositions(cnt),
                };
            }
            Debug.Log($"[ProceduralLevelGenerator] No timeline — fallback: {count} waves.");
            return waves;
        }

        private static EnemyType[] AvailableTypes(float songProgress)
        {
            var types = new List<EnemyType> { EnemyType.Seeker, EnemyType.TriangleMirror };
            if (songProgress >= 0.2f) types.Add(EnemyType.CylinderLauncher);
            if (songProgress >= 0.4f) { types.Add(EnemyType.SphereAerial); types.Add(EnemyType.CubeSplitter); }
            return types.ToArray();
        }

        private static EnemyType[] PickTypes(EnemyType[] available, int count)
        {
            var types = new EnemyType[count];
            for (int i = 0; i < count; i++)
                types[i] = available[Random.Range(0, available.Length)];
            return types;
        }

        private static Vector3[] PickSpawnPositions(int count)
        {
            float step       = (Mathf.PI * 2f) / count;
            float startAngle = Random.Range(0f, Mathf.PI * 2f);
            var   positions  = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                float angle  = startAngle + step * i + Random.Range(-0.35f, 0.35f);
                float radius = Random.Range(14f, 23f);
                positions[i] = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            }
            return positions;
        }
    }
}
