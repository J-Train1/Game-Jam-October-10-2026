using UnityEngine;

// Player settings, saved between sessions (PlayerPrefs) and applied instantly.
// Read from anywhere: GameSettings.Sensitivity, .Fx (effects volume), .Amb (ambience/music volume), etc.
// Anything that needs to react when a setting changes can listen to GameSettings.Changed.
public static class GameSettings
{
    public const float MinSensitivity = 0.25f, MaxSensitivity = 3f;
    public const float MinFov = 60f, MaxFov = 100f;

    public static float Sensitivity = 1f;      // multiplier on the player's base mouse sensitivity
    public static bool InvertY;
    public static float Master = 1f;           // 0..1
    public static float Effects = 1f;          // 0..1
    public static float Ambience = 1f;         // 0..1 (ambience + music)
    public static float Brightness;            // -1..1 (exposure offset, in stops)
    public static float Fov = 70f;
    public static bool HeadBob = true;
    public static float ScreenEffects = 1f;    // 0..1: film grain, lens fringing, dark edges
    public static bool ReduceFlashing;
    public static bool BrightnessSet;          // has the player been through the brightness screen?

    public static float Fx => Effects;
    public static float Amb => Ambience;

    public static event System.Action Changed;

    static bool loaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot() { Load(); Apply(); }

    public static void Load()
    {
        loaded = true;
        Sensitivity = PlayerPrefs.GetFloat("set.sensitivity", 1f);
        InvertY = PlayerPrefs.GetInt("set.invertY", 0) == 1;
        Master = PlayerPrefs.GetFloat("set.master", 1f);
        Effects = PlayerPrefs.GetFloat("set.effects", 1f);
        Ambience = PlayerPrefs.GetFloat("set.ambience", 1f);
        Brightness = PlayerPrefs.GetFloat("set.brightness", 0f);
        Fov = PlayerPrefs.GetFloat("set.fov", 70f);
        HeadBob = PlayerPrefs.GetInt("set.headBob", 1) == 1;
        ScreenEffects = PlayerPrefs.GetFloat("set.screenFx", 1f);
        ReduceFlashing = PlayerPrefs.GetInt("set.reduceFlashing", 0) == 1;
        BrightnessSet = PlayerPrefs.GetInt("set.brightnessSet", 0) == 1;
    }

    public static void Save()
    {
        PlayerPrefs.SetFloat("set.sensitivity", Sensitivity);
        PlayerPrefs.SetInt("set.invertY", InvertY ? 1 : 0);
        PlayerPrefs.SetFloat("set.master", Master);
        PlayerPrefs.SetFloat("set.effects", Effects);
        PlayerPrefs.SetFloat("set.ambience", Ambience);
        PlayerPrefs.SetFloat("set.brightness", Brightness);
        PlayerPrefs.SetFloat("set.fov", Fov);
        PlayerPrefs.SetInt("set.headBob", HeadBob ? 1 : 0);
        PlayerPrefs.SetFloat("set.screenFx", ScreenEffects);
        PlayerPrefs.SetInt("set.reduceFlashing", ReduceFlashing ? 1 : 0);
        PlayerPrefs.SetInt("set.brightnessSet", BrightnessSet ? 1 : 0);
        PlayerPrefs.Save();
    }

    /// <summary>Push the current values out (audio volume + anyone listening to Changed).</summary>
    public static void Apply()
    {
        if (!loaded) Load();
        AudioListener.volume = Mathf.Clamp01(Master);
        Changed?.Invoke();
    }

    public static void ResetDefaults()
    {
        Sensitivity = 1f; InvertY = false;
        Master = 1f; Effects = 1f; Ambience = 1f;
        Brightness = 0f; Fov = 70f; HeadBob = true; ScreenEffects = 1f; ReduceFlashing = false;
        Apply();
        Save();
    }
}
