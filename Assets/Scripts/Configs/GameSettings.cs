using UnityEngine;

public enum GameDifficulty
{
    Easy = 0,
    Hard = 1
}

public static class GameSettings
{
    private const string DifficultyKey = "GameSettings.Difficulty";
    private const string MusicVolumeKey = "GameSettings.MusicVolume";
    private const string PreferredControlSchemeKey = "GameSettings.PreferredControlScheme";
    private const int DefaultDifficultyValue = (int)GameDifficulty.Easy;
    private const float DefaultMusicVolume = 1f;
    private const string DefaultPreferredControlScheme = "Keyboard";

    public static GameDifficulty Difficulty
    {
        get => (GameDifficulty)PlayerPrefs.GetInt(DifficultyKey, DefaultDifficultyValue);
        set
        {
            PlayerPrefs.SetInt(DifficultyKey, (int)value);
            PlayerPrefs.Save();
        }
    }

    public static float MusicVolume
    {
        get => Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumeKey, DefaultMusicVolume));
        set
        {
            PlayerPrefs.SetFloat(MusicVolumeKey, Mathf.Clamp01(value));
            PlayerPrefs.Save();
        }
    }

    public static string PreferredControlScheme
    {
        get => PlayerPrefs.GetString(PreferredControlSchemeKey, DefaultPreferredControlScheme);
        set
        {
            PlayerPrefs.SetString(PreferredControlSchemeKey, string.IsNullOrWhiteSpace(value) ? DefaultPreferredControlScheme : value);
            PlayerPrefs.Save();
        }
    }
}