using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class SettingsScreen : MonoBehaviour, IUIScreen, ICancelHandler
{
    
    private GraphicsSettings candidateGraphics;          // what we’re previewing
    private static GraphicsSettings Clone(GraphicsSettings g) => new GraphicsSettings {
        width = g.width, height = g.height, refreshRate = g.refreshRate,
        windowMode = g.windowMode, qualityIndex = g.qualityIndex, vSync = g.vSync
    };
    
    [Header("Screen")]
    [SerializeField] private GameObject root;                 // Screen_Settings panel
    [SerializeField] private GameObject firstSelected;        // left nav "GAMEPLAY" button
    [SerializeField] private ScreenController screens;

    public string ScreenId => MenuIds.Options;
    public GameObject Root => root != null ? root : gameObject;
    public GameObject FirstSelected => firstSelected;

    // ---- Left Nav (tabs)
    [Header("Tabs")]
    [SerializeField] private Button tabGameplay;
    [SerializeField] private Button tabGraphics;
    [SerializeField] private Button tabAudio;

    [SerializeField] private GameObject panelGameplay;
    [SerializeField] private GameObject panelGraphics;
    [SerializeField] private GameObject panelAudio;

    enum Tab { Gameplay, Graphics, Audio }
    private Tab currentTab = Tab.Gameplay;

    // ---- Gameplay UI
    [Header("Gameplay")]
    [SerializeField] private Slider mouseSensitivity;
    [SerializeField] private Slider controllerSensitivity;
    [SerializeField] private Toggle invertYToggle;

    // ---- Graphics UI
    [Header("Graphics")]
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private TMP_Dropdown modeDropdown;     // Fullscreen, Borderless, Windowed
    [SerializeField] private TMP_Dropdown qualityDropdown;  // Unity quality levels
    [SerializeField] private Toggle vsyncToggle;
    [SerializeField] private Button applyGraphicsButton;

    // ---- Audio UI
    [Header("Audio")]
    [SerializeField] private Slider masterVolume;

    // ---- Confirm modal for graphics
    [Header("Confirm Modal (Graphics)")]
    [SerializeField] private GameObject confirmRoot;        // small panel "Keep these settings?"
    [SerializeField] private TMP_Text   confirmCountdownLabel;
    [SerializeField] private Button     confirmYesButton;
    [SerializeField] private Button     confirmNoButton;
    [SerializeField] private float      confirmSeconds = 10f;

    // Back button (optional)
    [SerializeField] private Button backButton;

    // Resolution list helper
    private struct Res { public int w,h,rr; public override string ToString()=> $"{w} x {h} @{rr}Hz"; }
    private List<Res> resolutions = new();
    private GraphicsSettings pendingGraphicsBeforeConfirm; // to revert
    private Coroutine confirmCo;

    // ---------- Lifecycle ----------
    void Awake()
    {
        if (root == null) root = gameObject;

        tabGameplay?.onClick.AddListener(() => ShowTab(Tab.Gameplay));
        tabGraphics?.onClick.AddListener(() => ShowTab(Tab.Graphics));
        tabAudio?.onClick.AddListener(() => ShowTab(Tab.Audio));
        confirmYesButton?.onClick.AddListener(OnConfirmYes);
        confirmNoButton ?.onClick.AddListener(OnConfirmNo);
        backButton?.onClick.AddListener(OnBack);

        // Gameplay hooks
        // --- Gameplay sliders are 0..100 in UI, stored as 0..1 ---
        if (mouseSensitivity)
        {
            mouseSensitivity.wholeNumbers = true;
            mouseSensitivity.minValue = 0; mouseSensitivity.maxValue = 100;
            mouseSensitivity.onValueChanged.AddListener(v =>
            {
                SettingsService.Gameplay.mouseSensitivity = v / 100f; // map 0..100 -> 0..1
                SettingsService.ApplyGameplay();
                SettingsService.Save();
            });
        }
        if (controllerSensitivity)
        {
            controllerSensitivity.wholeNumbers = true;
            controllerSensitivity.minValue = 0; controllerSensitivity.maxValue = 100;
            controllerSensitivity.onValueChanged.AddListener(v =>
            {
                SettingsService.Gameplay.controllerSensitivity = v / 100f;
                SettingsService.ApplyGameplay();
                SettingsService.Save();
            });
        }
        if (invertYToggle)
        {
            invertYToggle.onValueChanged.AddListener(v =>
            {
                SettingsService.Gameplay.invertY = v;
                SettingsService.ApplyGameplay();
                SettingsService.Save();
            });
        }


        // Audio hooks
        if (masterVolume)
        {
            masterVolume.wholeNumbers = true;
            masterVolume.minValue = 0; masterVolume.maxValue = 100;

            masterVolume.onValueChanged.AddListener(v =>
            {
                SettingsService.Audio.masterVolume = v / 100f; // 0..100 UI -> 0..1 stored
                SettingsService.ApplyAudio();
                SettingsService.Save();
            });
        }

        // Graphics hooks
        applyGraphicsButton?.onClick.AddListener(ApplyGraphicsWithConfirm);
    }

    public void OnShow(object args)
    {
        // Initial focus
        if (FirstSelected) EventSystem.current.SetSelectedGameObject(FirstSelected);

        // Populate UI from settings
        RefreshGameplayUI();
        RefreshAudioUI();
        BuildResolutionList();
        BuildQualityList();
        RefreshGraphicsUI();

        ShowTab(Tab.Gameplay); // first tab
    }

    public void OnHide()
    {
        if (confirmCo != null) StopCoroutine(confirmCo);
        confirmCo = null;
        if (confirmRoot) confirmRoot.SetActive(false);
    }

    public void OnCancel(BaseEventData eventData) => OnBack();

    public void OnBack()
    {
        if (confirmRoot && confirmRoot.activeSelf) { OnConfirmNo(); return; }
        screens?.Pop();
    }

    // ---------- Tabs ----------
    private void ShowTab(Tab t)
    {
        currentTab = t;
        panelGameplay?.SetActive(t == Tab.Gameplay);
        panelGraphics?.SetActive(t == Tab.Graphics);
        panelAudio?.SetActive(t == Tab.Audio);

        // Ensure focus lands inside the active panel (great for gamepad)
        if (t == Tab.Gameplay && mouseSensitivity) EventSystem.current.SetSelectedGameObject(mouseSensitivity.gameObject);
        if (t == Tab.Graphics && resolutionDropdown) EventSystem.current.SetSelectedGameObject(resolutionDropdown.gameObject);
        if (t == Tab.Audio && masterVolume) EventSystem.current.SetSelectedGameObject(masterVolume.gameObject);
    }

    // ---------- Gameplay ----------
    private void RefreshGameplayUI()
    {
        if (mouseSensitivity)
            mouseSensitivity.SetValueWithoutNotify(SettingsService.Gameplay.mouseSensitivity * 100f);

        if (controllerSensitivity)
            controllerSensitivity.SetValueWithoutNotify(SettingsService.Gameplay.controllerSensitivity * 100f);

        if (invertYToggle)
            invertYToggle.SetIsOnWithoutNotify(SettingsService.Gameplay.invertY);
    }
    
    //---------- Reset Gameplay Defaults ------------
    public void RestoreGameplayDefaults()
    {
        SettingsService.Gameplay.mouseSensitivity      = 1.0f; // 100%
        SettingsService.Gameplay.controllerSensitivity = 0.5f; // 50%
        SettingsService.Gameplay.invertY               = false;

        SettingsService.ApplyGameplay();
        SettingsService.Save();
        RefreshGameplayUI();
    }
    
    //----------- Reset Audio Defaults -------------
    public void RestoreAudioDefaults()
    {
        SettingsService.Audio.masterVolume = 1.0f; // 100%
        SettingsService.ApplyAudio();
        SettingsService.Save();
        RefreshAudioUI();
    }

    // ---------- Audio ----------
    private void RefreshAudioUI()
    {
        if (masterVolume)
            masterVolume.SetValueWithoutNotify(SettingsService.Audio.masterVolume * 100f);
    }

    // ---------- Graphics ----------
    private void BuildResolutionList()
    {
        resolutions.Clear();
        var seen = new HashSet<string>();
        foreach (var r in Screen.resolutions)
        {
            string key = $"{r.width}x{r.height}@{r.refreshRate}";
            if (seen.Contains(key)) continue;
            seen.Add(key);
            resolutions.Add(new Res{ w=r.width, h=r.height, rr=r.refreshRate });
        }
        resolutions.Sort((a,b) => a.w!=b.w? a.w.CompareTo(b.w) : (a.h!=b.h? a.h.CompareTo(b.h): a.rr.CompareTo(b.rr)));

        if (resolutionDropdown)
        {
            resolutionDropdown.ClearOptions();
            var opts = new List<string>(resolutions.Count);
            foreach (var r in resolutions) opts.Add($"{r.w} x {r.h} @ {r.rr}Hz");
            resolutionDropdown.AddOptions(opts);
        }
    }

    private void BuildQualityList()
    {
        if (!qualityDropdown) return;
        qualityDropdown.ClearOptions();
        qualityDropdown.AddOptions(new List<string>(QualitySettings.names));
    }

    private void RefreshGraphicsUI()
    {
        // Resolution
        if (resolutionDropdown)
        {
            int idx = resolutions.FindIndex(r => r.w == SettingsService.Graphics.width && r.h == SettingsService.Graphics.height && r.rr == SettingsService.Graphics.refreshRate);
            if (idx < 0) idx = 0;
            resolutionDropdown.SetValueWithoutNotify(idx);
        }

        // Window mode
        if (modeDropdown)
        {
            // 0 = Fullscreen, 1 = Borderless, 2 = Windowed (match your dropdown)
            int modeIndex = SettingsService.Graphics.windowMode switch
            {
                FullScreenMode.ExclusiveFullScreen => 0,
                FullScreenMode.FullScreenWindow    => 1,
                _                                   => 2,
            };
            modeDropdown.SetValueWithoutNotify(modeIndex);
        }

        // Quality & VSync
        if (qualityDropdown) qualityDropdown.SetValueWithoutNotify(Mathf.Clamp(SettingsService.Graphics.qualityIndex, 0, QualitySettings.names.Length - 1));
        if (vsyncToggle)     vsyncToggle.SetIsOnWithoutNotify(SettingsService.Graphics.vSync);
    }

    public void ApplyGraphicsWithConfirm()
    {
        // Read UI → candidate settings
        var g = new GraphicsSettings
        {
            width  = SettingsService.Graphics.width,
            height = SettingsService.Graphics.height,
            refreshRate = SettingsService.Graphics.refreshRate,
            windowMode  = SettingsService.Graphics.windowMode,
            qualityIndex= SettingsService.Graphics.qualityIndex,
            vSync       = SettingsService.Graphics.vSync
        };

        if (resolutionDropdown && resolutionDropdown.value >= 0 && resolutionDropdown.value < resolutions.Count)
        {
            var r = resolutions[resolutionDropdown.value];
            g.width = r.w; g.height = r.h; g.refreshRate = r.rr;
        }
        if (modeDropdown)
        {
            g.windowMode = modeDropdown.value switch
            {
                0 => FullScreenMode.ExclusiveFullScreen,
                1 => FullScreenMode.FullScreenWindow,
                _ => FullScreenMode.Windowed
            };
        }
        if (qualityDropdown) g.qualityIndex = qualityDropdown.value;
        if (vsyncToggle)     g.vSync        = vsyncToggle.isOn;

        // Save "before" as a deep copy so we can revert safely
        pendingGraphicsBeforeConfirm = Clone(SettingsService.Graphics);

        // Preview candidate (do NOT assign to SettingsService.Graphics yet)
        candidateGraphics = g;
        SettingsService.ApplyGraphics(candidateGraphics);

        // Open confirm modal
        if (confirmCo != null) StopCoroutine(confirmCo);
        if (confirmRoot) confirmRoot.SetActive(true);
        confirmCo = StartCoroutine(CoConfirmGraphics());

        // Put focus on Yes for pad users
        if (confirmYesButton)
            EventSystem.current.SetSelectedGameObject(confirmYesButton.gameObject);
    }

    private IEnumerator CoConfirmGraphics()
    {
        float t = confirmSeconds;
        while (t > 0f)
        {
            if (confirmCountdownLabel) confirmCountdownLabel.text = $"Keep these settings? ({Mathf.CeilToInt(t)}s)";
            t -= Time.unscaledDeltaTime;
            yield return null;
        }
        // Timeout = revert
        RevertGraphics();
    }

    public void OnConfirmYes()
    {
        if (confirmCo != null) StopCoroutine(confirmCo);
        confirmCo = null;
        if (confirmRoot) confirmRoot.SetActive(false);

        // Commit the candidate and save
        SettingsService.Graphics = Clone(candidateGraphics);
        SettingsService.Save();
    }

    public void OnConfirmNo() => RevertGraphics();

    private void RevertGraphics()
    {
        if (confirmCo != null) StopCoroutine(confirmCo);
        confirmCo = null;
        if (confirmRoot) confirmRoot.SetActive(false);

        // Revert to the saved "before" values (no save, we didn’t change them)
        SettingsService.ApplyGraphics(pendingGraphicsBeforeConfirm);
        RefreshGraphicsUI();

        // Return focus to Apply button for convenience
        if (applyGraphicsButton)
            EventSystem.current.SetSelectedGameObject(applyGraphicsButton.gameObject);
    }
}
