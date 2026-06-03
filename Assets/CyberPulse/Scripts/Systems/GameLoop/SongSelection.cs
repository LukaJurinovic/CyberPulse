namespace CyberPulse.Systems
{
    /// <summary>
    /// Carries the player's chosen song from the main menu into the gameplay scene.
    /// A plain static survives the SceneManager.LoadScene transition (it is not a
    /// MonoBehaviour, so it is never destroyed with the scene).
    ///
    /// <see cref="SongName"/> is matched by name against the gameplay scene's bundled
    /// clip array in LoadingController. Null/empty means "use the scene default"
    /// (the first bundled clip), so the game is still playable if launched directly
    /// into the gameplay scene without going through the menu.
    /// </summary>
    public static class SongSelection
    {
        /// <summary>Name of a bundled AudioClip. Matched by name in LoadingController._songs.</summary>
        public static string SongName;

        /// <summary>Absolute path to a user-imported audio file. Takes priority over SongName when set.</summary>
        public static string FilePath;
    }
}
