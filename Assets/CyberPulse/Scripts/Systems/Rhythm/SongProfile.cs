namespace CyberPulse.Systems
{
    public struct SongProfile
    {
        public float   BPM;
        public float   BeatInterval;
        public float   Duration;
        public float   AverageEnergy;
        public float   EnergyVariance;
        public float[] EnergyTimeline;
        public float   PeakEnergy;

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
