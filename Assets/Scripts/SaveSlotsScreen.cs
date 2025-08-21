using System.Collections.Generic;
using System.Threading.Tasks;
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

    [Header("List")]
    [SerializeField] SaveSlotView itemPrefab;
    [SerializeField] RectTransform content;
    [SerializeField, Range(2, 12)] int slotsShown = SaveSystem.MaxSlots;

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

        BuildIfNeeded();
        RefreshAll(a?.fallbackChapterId ?? "Prologue");

        // Decide what should be selected when the screen is active
        GameObject candidate = null;

        // Prefer the first visible slot's Button (if any)
        var firstSlotBtn = content != null
            ? content.GetComponentInChildren<Button>(includeInactive: false)
            : null;

        if (firstSlotBtn != null && firstSlotBtn.interactable)
            candidate = firstSlotBtn.gameObject;
        else if (backButton != null && backButton.interactable)
            candidate = backButton.gameObject;
        else if (firstSelected != null)
            candidate = firstSelected; // inspector fallback

        // Expose to ScreenController (it will select this after fade-in)
        firstSelected = candidate;

        // Optional: select immediately (do this at most once)
        if (EventSystem.current != null && firstSelected != null)
            EventSystem.current.SetSelectedGameObject(firstSelected);
    }


    public void OnHide() { }

    void BuildIfNeeded()
    {
        if (_views.Count > 0) return;
        for (int i = 0; i < slotsShown; i++)
        {
            var v = Instantiate(itemPrefab, content);
            v.Init(this, i);
            _views.Add(v);
        }
    }

    void RefreshAll(string fallbackChapterId)
    {
        for (int i = 0; i < _views.Count; i++)
        {
            var data = SaveSystem.LoadFromSlot(i);
            // Ensure autosave shows something sensible even if missing
            if (i == SaveSystem.AutoSlot && data == null && SaveSystem.SlotExists(i))
                data = SaveSystem.LoadFromSlot(i);

            _views[i].Bind(data, _mode);
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
