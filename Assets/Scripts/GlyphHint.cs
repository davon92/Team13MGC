using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GlyphHint : MonoBehaviour
{
    public enum HintKind { Nav, Select, Change, Confirm, Back, Start, Pause }

    [Header("UI")]
    [SerializeField] Image icon;
    [SerializeField] TMP_Text label;

    [Header("Behaviour")]
    [SerializeField] HintKind kind = HintKind.Confirm;
    [SerializeField] string customLabel = "";

    // cache the delegate so we can reliably unsubscribe
    System.Action<InputGlyphStyle> _onStyleChangedHandler;

    void OnEnable()
    {
        _onStyleChangedHandler = OnGlyphStyleChanged;
        SettingsService.OnGlyphStyleChanged += _onStyleChangedHandler;
        Apply(); // pick up current style immediately
    }

    void OnDisable()
    {
        if (_onStyleChangedHandler != null)
        {
            SettingsService.OnGlyphStyleChanged -= _onStyleChangedHandler;
            _onStyleChangedHandler = null;
        }
    }

    void OnGlyphStyleChanged(InputGlyphStyle _) => Apply();

    public void Refresh()
    {
        // If we’re not active, don’t try to rebuild/layout now.
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
            return;

        if (label) label.ForceMeshUpdate();

        StartCoroutine(RebuildNextFrame());
    }

    System.Collections.IEnumerator RebuildNextFrame()
    {
        yield return null;                        // wait for TMP to finish measuring
        Canvas.ForceUpdateCanvases();
        var rt = (RectTransform)transform;
        LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        var parent = rt.parent as RectTransform;
        if (parent) LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
    }

    void Apply()
    {
        var g = GlyphLibrary.Current;
        if (!g || !icon) return;

        switch (kind)
        {
            case HintKind.Nav:     icon.sprite = g.uiFullNav; break;
            case HintKind.Change:  icon.sprite = g.uiChange;  break;
            case HintKind.Select:  icon.sprite = g.uiSelect;  break;
            case HintKind.Confirm: icon.sprite = g.uiSubmit;  break;
            case HintKind.Back:    icon.sprite = g.uiCancel;  break;
            case HintKind.Start:
            case HintKind.Pause:   icon.sprite = g.start;     break;
        }

        if (label)
            label.text = string.IsNullOrWhiteSpace(customLabel)
                ? DefaultLabel(kind)
                : customLabel;

        Refresh();
    }

    static string DefaultLabel(HintKind k) => k switch
    {
        HintKind.Select  => ":Select",
        HintKind.Change  => ":Change",
        HintKind.Nav     => ":Select",
        HintKind.Confirm => ":Confirm",
        HintKind.Back    => ":Return",
        HintKind.Start   => ":Start",
        HintKind.Pause   => ":Pause",
        _                => ""
    };
}
