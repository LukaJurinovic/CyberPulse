namespace CyberPulse.Systems
{
    public struct SongProfile
    {
        public float   BPM;
        public float   BeatInterval;     // 60f / BPM
        public float   Duration;         // seconds
        public float   AverageEnergy;    // 0-1
        public float   EnergyVariance;   // stddev of timeline energies
        public float[] EnergyTimeline;   // RMS sampled every 0.5s
        public float   PeakEnergy;       // maximum energy seen

        public static SongProfile Fallback(float duration) => new SongProfile
        {
            BPM            = 120f,
            BeatInterval   = 0.5f,
            Duration       = duration,
            AverageEnergy  = 0.5f,
            EnergyVariance = 0.3f,
            EnergyTimeline = new float[0],
            PeakEnergy     = 0.5f,
        };
    }
}
