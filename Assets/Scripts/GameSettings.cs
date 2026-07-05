// Shared settings keys and session-mode state.
public static class GameSettings
{
    public const string SensPrefKey = "MouseSensitivity";
    public const string VolumePrefKey = "MasterVolume";
    public const float DefaultSens = 5f;
    public const float DefaultVolume = 1f;

    // True when the game was launched from the lobby START button (no online
    // session); multiplayer entry points reset it.
    public static bool SinglePlayer;
}
