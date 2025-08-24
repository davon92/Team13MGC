using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SaveSlotView : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] Button   button;
    [SerializeField] TMP_Text title;
    [SerializeField] TMP_Text subtitle;

    int _slot;
    bool _hasData;
    SaveSlotsScreen _owner;

    void Awake()
    {
        button = GetComponent<Button>();
        if (!title)    title    = transform.Find("Title")?.GetComponent<TMP_Text>();
        if (!subtitle) subtitle = transform.Find("Sub")?.GetComponent<TMP_Text>();

        if (button) button.onClick.AddListener(HandleClick); // <-- rename here
    }

    public void Init(SaveSlotsScreen owner, int slot)
    {
        _owner = owner;
        _slot  = slot;
    }

    public void Bind(SaveSystem.ProfileData data, SaveSlotsScreen.Mode mode)
    {
        _hasData = (data != null);

        if (title)    title.text    = $"SLOT {_slot}";
        if (subtitle) subtitle.text = _hasData
            ? $"{data.note} • {SaveSystem.ToDateString(data.savedAtUtcTicks)}"
            : (mode == SaveSlotsScreen.Mode.Save ? "Empty (Save here)" : "Empty");

        if (button)
            button.interactable = (mode == SaveSlotsScreen.Mode.Load) ? _hasData : true;
    }

    void HandleClick() => _owner?.HandleSlotClicked(_slot, _hasData);
}