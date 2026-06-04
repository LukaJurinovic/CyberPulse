using UnityEngine;

namespace CyberPulse.Systems
{
    /// <summary>
    /// Carries the player's chosen song from the main menu into the gameplay scene.
    /// A plain static survives the SceneManager.LoadScene transition (it is not a
    /// MonoBehaviour, so it is never destroyed with the scene).
    ///
    /// For bundled tracks the menu hands over the actual <see cref="Clip"/> reference,
    /// so the exact clip the player picked is the one that plays — no name matching.
    /// <see cref="SongName"/> remains as a name-match fallback (and loading-screen label)
    /// for when the gameplay scene is launched directly without going through the menu.
    /// </summary>
    public static class SongSelection
    {
        /// <summary>The chosen bundled AudioClip, carried directly from the menu. Takes priority over SongName.</summary>
        public static AudioClip Clip;

        /// <summary>Name of a bundled AudioClip. Name-match fallback in LoadingController._songs when Clip is null.</summary>
        public static string SongName;

        /// <summary>Absolute path to a user-imported audio file. Takes priority over Clip/SongName when set.</summary>
        public static string FilePath;
    }
}
