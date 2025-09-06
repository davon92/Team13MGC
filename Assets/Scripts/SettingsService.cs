using UnityEngine;
using UnityEngine.Audio;

public enum InputGlyphStyle { Auto, Xbox, PlayStation,Nintendo,KeyboardMouse }

[System.Serializable]
public class GameplaySettings
{
    public float mouseSensitivity = 1.0f;      // 0..1
    public float controllerSensitivity = 0.5f; // 0..1
    public bool invertY = false;

    // NEW: which button glyphs to show in UI
    public InputGlyphStyle glyphStyle = InputGlyphStyle.Auto;
}

[System.Serializable]
public class AudioSettings
{
    public float masterVolume = 1.0f;          // 0..1
}

[System.Serializable]
public class GraphicsSettings
{
    public int width;
    public int height;
    public int refreshRate;                    // Hz
    public FullScreenMode windowMode = FullScreenMode.FullScreenWindow;
    public int qualityIndex = 2;               // Unity quality level index
    public bool vSync = true;
}

public static class SettingsService
{
    // Assign this once (e.g., in a bootstrapper, or from inspector on a tiny scene object)
    // The exposed parameter on the mixer must be named "MasterVolume" (in dB).
    public static AudioMixer Mixer;

    public static GameplaySettings Gameplay { get; private set; } = new();
    public static AudioSettings    Audio    { get; private set; } = new();
    public static GraphicsSettings Graphics { get; set; }         = new();

    const string KeyGameplay = "settings_gameplay";
    const string KeyAudio    = "settings_audio";
    const string KeyGraphics = "settings_graphics";

    // --- NEW: auto-detected glyph style cache and event ---
    static InputGlyphStyle _autoDetectedStyle = InputGlyphStyle.KeyboardMouse;

    /// Consumers should use this when deciding what to display.
    public static InputGlyphStyle EffectiveGlyphStyle =>
        (Gameplay.glyphStyle == InputGlyphStyle.Auto) ? _autoDetectedStyle : Gameplay.glyphStyle;

    /// Fired when either the user changes the dropdown OR Auto detects a different device.
    public static event System.Action<InputGlyphStyle> OnGlyphStyleChanged;

    // ---- Persistence ----
    public static void Load()
    {
        bool firstRun = !PlayerPrefs.HasKey(KeyGameplay)
                     && !PlayerPrefs.HasKey(KeyAudio)
                     && !PlayerPrefs.HasKey(KeyGraphics);

        Gameplay = JsonSafe<PlayerPrefsWrapper, GameplaySettings>(KeyGameplay) ?? new GameplaySettings();
        Audio    = JsonSafe<PlayerPrefsWrapper, AudioSettings>(KeyAudio)       ?? new AudioSettings();
        Graphics = JsonSafe<PlayerPrefsWrapper, GraphicsSettings>(KeyGraphics) ?? DefaultGraphics();

        ApplyGameplay();
        ApplyAudio(); // don’t auto-apply graphics; apply on “Apply” button

        if (firstRun) Save();
    }

    public static void Save()
    {
        PlayerPrefs.SetString(KeyGameplay, JsonUtility.ToJson(Gameplay));
        PlayerPrefs.SetString(KeyAudio,    JsonUtility.ToJson(Audio));
        PlayerPrefs.SetString(KeyGraphics, JsonUtility.ToJson(Graphics));
        PlayerPrefs.Save();
    }

    // ---- Apply live ----
    public static void ApplyGameplay()
    {
        // Notify listeners with the EFFECTIVE style (Auto resolves to detected)
        OnGlyphStyleChanged?.Invoke(EffectiveGlyphStyle);
    }

    public static void ApplyAudio()
    {
        if (Mixer == null) return;

        // Map 0..1 to decibels (log curve). 1 -> 0dB, 0.5 -> -6.02dB, 0 -> minDb
        float dB = Linear01ToDecibels(Audio.masterVolume, -80f);
        Mixer.SetFloat("MasterVolume", dB);
    }

    public static void ApplyGraphics(GraphicsSettings g)
    {
        Screen.SetResolution(g.width, g.height, g.windowMode, g.refreshRate);
        QualitySettings.vSyncCount = g.vSync ? 1 : 0;
        QualitySettings.SetQualityLevel(
            Mathf.Clamp(g.qualityIndex, 0, QualitySettings.names.Length - 1),
            true);
    }

    // ---- NEW: called by our watcher whenever a different device was used ----
    public static void SetAutoDetectedGlyphStyle(InputGlyphStyle detected)
    {
        if (_autoDetectedStyle == detected) return;
        _autoDetectedStyle = detected;

        // Only propagate if we're in Auto; if the user forced a style, don't override it.
        if (Gameplay.glyphStyle == InputGlyphStyle.Auto)
            OnGlyphStyleChanged?.Invoke(EffectiveGlyphStyle);
    }

    // ---- Helpers ----
    public static float Linear01ToDecibels(float v, float minDb = -80f)
    {
        v = Mathf.Clamp01(v);
        if (v <= 0.0001f) return minDb;
        return Mathf.Log10(v) * 20f;
    }

    private static GraphicsSettings DefaultGraphics()
    {
        var cur = Screen.currentResolution;
        return new GraphicsSettings
        {
            width = cur.width,
            height = cur.height,
            refreshRate = cur.refreshRate,
            windowMode = FullScreenMode.FullScreenWindow,
            qualityIndex = QualitySettings.GetQualityLevel(),
            vSync = QualitySettings.vSyncCount > 0
        };
    }

    // Small helper to avoid null on bad/missing JSON
    private static TOut JsonSafe<TWrapper, TOut>(string key) where TOut : class
    {
        var json = PlayerPrefs.GetString(key, "");
        if (string.IsNullOrEmpty(json)) return null;
        try { return JsonUtility.FromJson<TOut>(json); }
        catch { return null; }
    }

    // Dummy wrapper type (not used, keeps generic signature simple)
    private class PlayerPrefsWrapper { }
}
