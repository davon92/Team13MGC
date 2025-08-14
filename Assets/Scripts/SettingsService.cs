using UnityEngine;
using UnityEngine.Audio;

[System.Serializable]
public class GameplaySettings
{
    public float mouseSensitivity = 0.5f;     // 0.1–2.0
    public float controllerSensitivity = 0.5f;// 0.1–2.0
    public bool invertY = false;
}

[System.Serializable]
public class AudioSettings
{
    public float masterVolume = 1.0f;         // 0–1
}

[System.Serializable]
public class GraphicsSettings
{
    public int width;
    public int height;
    public int refreshRate;                   // Hz
    public FullScreenMode windowMode = FullScreenMode.FullScreenWindow; // Borderless default
    public int qualityIndex = 2;              // Unity Quality level index
    public bool vSync = true;
}

public static class SettingsService
{
    // Audio
    public static AudioMixer Mixer; // assign in Bootstrap or via inspector to expose "MasterVolume" (in dB)

    public static GameplaySettings Gameplay { get; private set; } = new();
    public static AudioSettings    Audio    { get; private set; } = new();
    public static GraphicsSettings Graphics { get; set; } = new();

    const string KeyGameplay = "settings_gameplay";
    const string KeyAudio    = "settings_audio";
    const string KeyGraphics = "settings_graphics";

    public static void Load()
    {
        bool firstRun = !PlayerPrefs.HasKey(KeyGameplay)
                        && !PlayerPrefs.HasKey(KeyAudio)
                        && !PlayerPrefs.HasKey(KeyGraphics);

        Gameplay = JsonUtility.FromJson<GameplaySettings>(PlayerPrefs.GetString(KeyGameplay, "")) ?? new GameplaySettings();
        Audio    = JsonUtility.FromJson<AudioSettings>(PlayerPrefs.GetString(KeyAudio, ""))       ?? new AudioSettings();
        Graphics = JsonUtility.FromJson<GraphicsSettings>(PlayerPrefs.GetString(KeyGraphics, "")) ?? DefaultGraphics();

        ApplyGameplay();
        ApplyAudio();
        // Don't auto-apply graphics here.

        if (firstRun) Save(); // commit defaults so next boot restores the same values
    }

    public static void Save()
    {
        PlayerPrefs.SetString(KeyGameplay, JsonUtility.ToJson(Gameplay));
        PlayerPrefs.SetString(KeyAudio,    JsonUtility.ToJson(Audio));
        PlayerPrefs.SetString(KeyGraphics, JsonUtility.ToJson(Graphics));
        PlayerPrefs.Save();
    }

    public static void ApplyGameplay()
    {
        // Expose these to your input/controller systems later.
        // For now they just live in SettingsService.Gameplay and consumers read them.
    }

    public static void ApplyAudio()
    {
        if (Mixer == null) return;

        // Slider is 0..1 (stored). Convert to decibels with a log curve.
        float dB = Linear01ToDecibels(Audio.masterVolume, -80f);
        Mixer.SetFloat("MasterVolume", dB); // make sure this exposed param exists
    }
    
    /// <summary>
    /// Maps 0..1 to decibels using a log curve. 1 -> 0 dB, 0.5 -> -6.02 dB, 0 -> minDb.
    /// </summary>
    public static float Linear01ToDecibels(float v, float minDb = -80f)
    {
        v = Mathf.Clamp01(v);
        if (v <= 0.0001f) return minDb;               // effectively silent
        return Mathf.Log10(v) * 20f;                  // classic audio mapping
    }

    public static void ApplyGraphics(GraphicsSettings g)
    {
        // Apply resolution + mode
        Screen.SetResolution(g.width, g.height, g.windowMode, g.refreshRate);
        QualitySettings.vSyncCount = g.vSync ? 1 : 0;
        QualitySettings.SetQualityLevel(Mathf.Clamp(g.qualityIndex, 0, QualitySettings.names.Length - 1), true);
    }

    private static GraphicsSettings DefaultGraphics()
    {
        var cur = Screen.currentResolution;
        return new GraphicsSettings {
            width = cur.width, height = cur.height, refreshRate = cur.refreshRate,
            windowMode = FullScreenMode.FullScreenWindow, qualityIndex = QualitySettings.GetQualityLevel(), vSync = QualitySettings.vSyncCount > 0
        };
    }
}
