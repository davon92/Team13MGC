using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

public partial class SettingsScreen : MonoBehaviour, IUIScreen
{
    public string ScreenId => "Options";
    public GameObject Root => root;
    public GameObject FirstSelected => firstSelected;

    [Header("Refs")]
    [SerializeField] private GameObject root;
    [SerializeField] private GameObject firstSelected;
    [SerializeField] private ScreenController screens;   // ← drag your ScreenController here

    [Header("Tabs")]
    [SerializeField] private Button tabGameplay;
    [SerializeField] private Button tabGraphics;
    [SerializeField] private Button tabAudio;

    [SerializeField] private GameObject panelGameplay;
    [SerializeField] private GameObject panelGraphics;
    [SerializeField] private GameObject panelAudio;

    // Panel focus targets
    [SerializeField] private Slider         mouseSensitivity;
    [SerializeField] private TMP_Dropdown   resolutionDropdown;
    [SerializeField] private Slider         masterVolume;
    // NEW: Button UI / Controller Icon style
    [SerializeField] private TMP_Dropdown   glyphStyleDropdown;
    
    // The underline group for the tab buttons
    [SerializeField] private UISelectScalerGroup leftNavGroup;

    private enum Tab { Gameplay, Graphics, Audio }
    private Tab currentTab = Tab.Gameplay;

    void Awake()
    {
        if (!leftNavGroup) leftNavGroup = Root.GetComponentInChildren<UISelectScalerGroup>(true);
        leftNavGroup?.SetKeepLastWhenOutside(true);

        // Tabs (existing)
        WireTab(tabGameplay, Tab.Gameplay, mouseSensitivity ? mouseSensitivity.gameObject : null);
        WireTab(tabGraphics, Tab.Graphics, resolutionDropdown ? resolutionDropdown.gameObject : null);
        WireTab(tabAudio,    Tab.Audio,    masterVolume ? masterVolume.gameObject : null);

        // NEW: build dropdown once
        if (glyphStyleDropdown)
        {
            glyphStyleDropdown.ClearOptions();
            glyphStyleDropdown.AddOptions(new List<string> {
                "Auto", "Xbox", "PlayStation", "Nintendo", "Keyboard & Mouse"
            });
            glyphStyleDropdown.onValueChanged.AddListener(OnGlyphStyleChanged);
        }
    }

    void WireTab(Button tabBtn, Tab tab, GameObject panelFocus)
    {
        if (!tabBtn) return;

        // 1) When the tab gets selected (via hover/controller), keep underline on that tab.
        var pin = tabBtn.gameObject.GetComponent<PinUnderlineOnSelect>();
        if (!pin) pin = tabBtn.gameObject.AddComponent<PinUnderlineOnSelect>();
        pin.Init(leftNavGroup);

        // 2) When the tab is clicked/submit, pin underline and move focus into panel.
        tabBtn.onClick.AddListener(() =>
        {
            leftNavGroup?.ForceSelect(tabBtn.transform, true);
            ShowTab(tab, moveFocusToPanel: panelFocus != null);
            if (panelFocus) EventSystem.current.SetSelectedGameObject(panelFocus);
        });
    }

    public void OnShow(object args)
    {
        var es = EventSystem.current;
        if (es)
        {
            var cur = es.currentSelectedGameObject;
            bool hasSaved = SelectionMemory.TryGet(ScreenId, out _);
            bool needFocus = cur == null || !cur.activeInHierarchy || !cur.transform.IsChildOf(Root.transform);
            if (needFocus && !hasSaved && FirstSelected) es.SetSelectedGameObject(FirstSelected);
        }

        if (!leftNavGroup) leftNavGroup = Root.GetComponentInChildren<UISelectScalerGroup>(true);
        leftNavGroup?.SetKeepLastWhenOutside(true);
        leftNavGroup?.SyncNow(instant: true);

        // Build/refresh UI without stealing focus from tabs
        RefreshGameplayUI();
        RefreshAudioUI();
        BuildResolutionList();
        BuildQualityList();
        RefreshGraphicsUI();

        // Show the current tab’s panel, but don't move selection into it
        ShowTab(currentTab, moveFocusToPanel: false);

        if (glyphStyleDropdown)
            glyphStyleDropdown.SetValueWithoutNotify((int)SettingsService.Gameplay.glyphStyle);
        
        // Also pin underline to the tab that’s currently selected (so it never disappears)
        var currentTabBtn = currentTab switch
        {
            Tab.Gameplay => tabGameplay,
            Tab.Graphics => tabGraphics,
            _            => tabAudio
        };
        if (currentTabBtn) leftNavGroup?.ForceSelect(currentTabBtn.transform, true);
    }
    
    void OnGlyphStyleChanged(int index)
    {
        SettingsService.Gameplay.glyphStyle = (InputGlyphStyle)index;
        SettingsService.ApplyGameplay(); // notifies listeners
        SettingsService.Save();          // persist immediately (optional)
    }

    public void OnHide() {}

    private void ShowTab(Tab t, bool moveFocusToPanel = false)
    {
        currentTab = t;
        panelGameplay?.SetActive(t == Tab.Gameplay);
        panelGraphics?.SetActive(t == Tab.Graphics);
        panelAudio?.SetActive(t == Tab.Audio);

        if (!moveFocusToPanel) return;

        // Focus into the panel only when explicitly requested by click/submit
        if (t == Tab.Gameplay && mouseSensitivity)
            EventSystem.current.SetSelectedGameObject(mouseSensitivity.gameObject);
        if (t == Tab.Graphics && resolutionDropdown)
            EventSystem.current.SetSelectedGameObject(resolutionDropdown.gameObject);
        if (t == Tab.Audio && masterVolume)
            EventSystem.current.SetSelectedGameObject(masterVolume.gameObject);
    }

    // ---- Cancel / Back (since there’s no back button) ----
    public void OnBack()
    {
        if (screens && screens.CanPop) screens.Pop();
    }

    // PlayerInput (Send Messages) maps UI/Cancel here
    public void OnUI_Cancel(InputValue v)
    {
        if (v.isPressed) OnBack();
    }

    // your existing methods (stubs here)
    void RefreshGameplayUI() { /* ... */ }
    void RefreshAudioUI()    { /* ... */ }
    void RefreshGraphicsUI() { /* ... */ }
    void BuildResolutionList(){ /* ... */ }
    void BuildQualityList()  { /* ... */ }
}
