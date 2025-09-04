using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class SaveSlotsScreen : MonoBehaviour, IUIScreen
{
    public enum Mode { Save, Load }

    public class Args
    {
        public Mode mode = Mode.Load;
        public string fallbackChapterId = "Prologue"; // when saving with no snapshot provided
    }

    [Header("Wiring")]
    [SerializeField] GameObject root;
    [SerializeField] GameObject firstSelected;
    [SerializeField] ScreenController screens;
    [SerializeField] Button backButton;
    [SerializeField] TMP_Text text;

    [Header("List")]
    [SerializeField] SaveSlotView itemPrefab;
    [SerializeField] RectTransform content;
    [SerializeField, Range(2, 12)] int slotsShown = SaveSystem.MaxSlots;
    [SerializeField] AutoSavePanel autoPanel;

    public string ScreenId => MenuIds.SaveLoad;
    public GameObject Root => root != null ? root : gameObject;
    public GameObject FirstSelected => firstSelected;

    Mode _mode = Mode.Load;
    readonly List<SaveSlotView> _views = new();

    void Awake()
    {
        if (backButton) backButton.onClick.AddListener(OnBack);
    }

    public void OnShow(object args)
    {
        var a = args as Args;
        _mode = a?.mode ?? Mode.Load;
        text.text = $"{_mode.ToString()} Game";
        BuildIfNeeded();
        RefreshAll(a?.fallbackChapterId ?? "Prologue");

        GameObject candidate = null;

        // Prefer autosave card if loading and it exists
        var auto = autoPanel ? SaveSystem.LoadFromSlot(SaveSystem.AutoSlot) : null;
        if (_mode == Mode.Load && auto != null && autoPanel)
            candidate = autoPanel.FirstSelectable;

        // Else first manual slot
        if (candidate == null)
        {
            var firstSlotBtn = content ? content.GetComponentInChildren<Button>(false) : null;
            if (firstSlotBtn && firstSlotBtn.interactable) candidate = firstSlotBtn.gameObject;
        }

        if (candidate == null && backButton && backButton.interactable) candidate = backButton.gameObject;
        if (candidate == null && firstSelected) candidate = firstSelected;

        firstSelected = candidate;
        if (EventSystem.current && firstSelected)
            EventSystem.current.SetSelectedGameObject(firstSelected);
        
        var grp = Root.GetComponentInChildren<UISelectScalerGroup>(true);
        if (grp) grp.SyncNow(instant: true);
        
    }

    void WireVerticalNav()
    {
        var btns = content.GetComponentsInChildren<Button>(includeInactive: false);
        for (int i = 0; i < btns.Length; i++)
        {
            var nav = btns[i].navigation;
            nav.mode = Navigation.Mode.Explicit;
            nav.selectOnUp   = i > 0 ? btns[i - 1] : null;
            nav.selectOnDown = i < btns.Length - 1 ? btns[i + 1] : null;
            btns[i].navigation = nav;
        }
    }

    public void OnHide() { }

    void BuildIfNeeded()
    {
        if (_views.Count > 0) return;

        int manualCount = Mathf.Clamp(slotsShown, 1, SaveSystem.MaxSlots - 1);
        for (int i = 0; i < manualCount; i++)
        {
            var v = Instantiate(itemPrefab, content);
            int slot = i + 1;              // 1..(MaxSlots-1)  ← no slot 0 here
            v.Init(this, slot);
            _views.Add(v);
        }
    }

    void RefreshAll(string fallbackChapterId)
    {
        for (int i = 0; i < _views.Count; i++)
        {
            int slot = i + 1; // 1..N
            var data = SaveSystem.LoadFromSlot(slot);
            _views[i].Bind(data, _mode);
        }

        // Bind autosave panel at the end (optional)
        if (autoPanel)
        {
            var auto = SaveSystem.LoadFromSlot(SaveSystem.AutoSlot);
            autoPanel.Bind(auto);
            autoPanel.ConfigureForMode(_mode);

            var btn = autoPanel.GetComponentInChildren<Button>(true);
            if (btn)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(async () =>
                {
                    if (_mode != Mode.Load || auto == null) return;
                    SaveSystem.QueueLoadSlot(SaveSystem.AutoSlot);
                    await SceneFlow.LoadVNAsync(false);
                });
            }
        }
    }

    public async void HandleSlotClicked(int slot, bool hasData)
    {
        if (_mode == Mode.Load)
        {
            if (!hasData) return;
            SaveSystem.QueueLoadSlot(slot);
            await SceneFlow.LoadVNAsync(false); // VNBootstrap will pick PendingLoadSlot
        }
        else // Save
        {
            if (slot == SaveSystem.AutoSlot) return; // don’t overwrite Auto from menu
            // Save quick state (you’ll replace this with real VN snapshot later)
            var existing = SaveSystem.LoadFromSlot(slot);
            var note = (existing == null) ? "Manual Save" : "Overwrite Save";
            var pd = new SaveSystem.ProfileData
            {
                chapterId = existing?.chapterId ?? "Prologue",
                note = note
            };
            SaveSystem.SaveToSlot(slot, pd);
            RefreshAll(pd.chapterId);
        }
    }

    public void OnBack() { if (screens.CanPop) screens.Pop(); else screens.Show(MenuIds.StoryMenu); }
    public void OnUI_Cancel(UnityEngine.InputSystem.InputValue v) { if (v.isPressed) OnBack(); }
}
