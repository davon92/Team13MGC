using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AutoSavePanel : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private CanvasGroup cg;
    [SerializeField] private Button loadButton;         // the focus target
    [SerializeField] private TMP_Text title;            // e.g. "Autosave"
    [SerializeField] private TMP_Text subtitle;      // e.g. "Day 2 - Morning  {Time Code}"

    public GameObject FirstSelectable => loadButton ? loadButton.gameObject : gameObject;

    bool hasData;

    void Reset() { cg = GetComponent<CanvasGroup>(); }

    public void BindEmpty()
    {
        hasData = false;
        if (title)       title.text       = "Autosave";
        if (subtitle) subtitle.text = "—";
    }

    public void Bind(SaveSystem.ProfileData data)
    {
        // IMPORTANT: write the FIELD, not a new local variable
        hasData = (data != null);

        if (title) title.text = "Autosave";

        if (subtitle)
        {
            if (hasData)
            {
                // Subtitle = chapter + time (no “Autosave” duplication)
                var chapter = string.IsNullOrEmpty(data.chapterId) ? "—" : data.chapterId;
                subtitle.text = $"{chapter} • {SaveSystem.ToDateString(data.savedAtUtcTicks)}";
            }
            else
            {
                subtitle.text = "—";
            }
        }
    }

    /// <summary>Enable/disable interactivity based on screen mode.</summary>
    public void ConfigureForMode(SaveSlotsScreen.Mode mode)
    {
        // Load: selectable only if we HAVE data. Save: never selectable.
        bool interactable = (mode == SaveSlotsScreen.Mode.Load) && hasData;

        if (loadButton) loadButton.interactable = interactable;

        if (cg)
        {
            cg.interactable   = interactable;
            cg.blocksRaycasts = interactable;
            cg.alpha          = interactable ? 1f : 0.75f;
        }
    }
}
