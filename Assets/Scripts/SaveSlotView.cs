using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SaveSlotView : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] Button button;
    [SerializeField] TMP_Text title;
    [SerializeField] TMP_Text subtitle;

    int _slot;
    bool _hasData;
    SaveSlotsScreen _owner;

    public void Init(SaveSlotsScreen owner, int slot)
    {
        _owner = owner;
        _slot  = slot;
        if (button) button.onClick.AddListener(OnClick);
    }

    public void Bind(SaveSystem.ProfileData data, SaveSlotsScreen.Mode mode)
    {
        _hasData = (data != null);
        var label = (_slot == SaveSystem.AutoSlot) ? "AUTO SAVE" : $"SLOT {_slot}";
        if (title) title.text = label;

        if (subtitle)
        {
            if (_hasData)
                subtitle.text = $"{data.note} • {SaveSystem.ToDateString(data.savedAtUtcTicks)}";
            else
                subtitle.text = (mode == SaveSlotsScreen.Mode.Save) ? "Empty (Save here)" : "Empty";
        }

        // Disable button for Load if no data; for Save, disable Auto (slot 0) if you want
        if (button)
        {
            if (mode == SaveSlotsScreen.Mode.Load)
                button.interactable = _hasData;
            else // Save
                button.interactable = (_slot != SaveSystem.AutoSlot); // don’t allow overwriting Auto from Pause menu
        }
    }

    void OnClick()
    {
        _owner?.HandleSlotClicked(_slot, _hasData);
    }
}