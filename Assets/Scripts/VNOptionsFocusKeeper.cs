using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class VNOptionsFocusKeeper : MonoBehaviour
{
    [Header("Wire these to your Options Presenter")]
    [SerializeField] Transform   optionsRoot;   // parent of Option Item clones
    [SerializeField] CanvasGroup optionsGroup;  // CanvasGroup the presenter drives

    // Remember BOTH label and index. Label is most robust after rebuilds.
    static string s_lastLabel;
    static int?   s_lastIndex;

    void Awake()
    {
        if (!optionsRoot) optionsRoot = transform;
        if (!optionsGroup) optionsGroup = GetComponentInParent<CanvasGroup>();
    }

    bool OptionsVisible =>
        optionsGroup
            ? optionsGroup.interactable && optionsGroup.blocksRaycasts && optionsGroup.alpha > 0.99f
            : (optionsRoot && optionsRoot.gameObject.activeInHierarchy);

    List<Selectable> CollectOptions(out List<string> labels)
    {
        var arr   = optionsRoot ? optionsRoot.GetComponentsInChildren<Selectable>(true) : new Selectable[0];
        var items = new List<Selectable>(arr.Length);
        labels    = new List<string>(arr.Length);
        foreach (var s in arr)
        {
            if (!s || !s.IsActive() || !s.interactable) continue;
            items.Add(s);
            var t = s.GetComponentInChildren<TMP_Text>(true);
            labels.Add(t ? t.text : null);
        }
        return items;
    }

    // --- Public static helpers to use from anywhere ---

    /// Call this RIGHT BEFORE opening Pause.
    public static void CaptureNow()
    {
        var k = FindFirstObjectByType<VNOptionsFocusKeeper>(FindObjectsInactive.Include);
        if (!k) return;

        if (!k.OptionsVisible) return;

        var es = EventSystem.current;
        if (!es) return;

        var list = k.CollectOptions(out var labels);
        if (list.Count == 0) return;

        var sel = es.currentSelectedGameObject;
        int idx = list.FindIndex(s => s && s.gameObject == sel);
        if (idx < 0) idx = Mathf.Clamp(s_lastIndex ?? 0, 0, list.Count - 1);

        s_lastIndex = idx;
        s_lastLabel = (idx >= 0 && idx < labels.Count) ? labels[idx] : null;
        // Debug.Log($"[VNOptionsFocusKeeper] Captured idx={s_lastIndex}, label='{s_lastLabel}'");
    }

    /// Call this AFTER closing Pause; waits until options are ready and restores focus.
    public static IEnumerator RestoreAfterResume(float maxWaitUnscaled = 0.75f)
    {
        var k = FindFirstObjectByType<VNOptionsFocusKeeper>(FindObjectsInactive.Include);
        if (!k) yield break;

        // Wait until the presenter has fully shown (alpha/interactable) and items exist
        float start = Time.unscaledTime;
        List<Selectable> list = null;
        List<string> labels = null;
        while (true)
        {
            if (!k) yield break;
            if (k.OptionsVisible)
            {
                list = k.CollectOptions(out labels);
                if (list.Count > 0) break;
            }
            if (Time.unscaledTime - start > maxWaitUnscaled) break;
            yield return null;
        }

        if (list == null || list.Count == 0) yield break;

        // Prefer label (survives reordering), fallback to index, then first item.
        int idx = -1;
        if (!string.IsNullOrEmpty(s_lastLabel))
            idx = labels.FindIndex(txt => string.Equals(txt, s_lastLabel, System.StringComparison.Ordinal));

        if (idx < 0 && s_lastIndex.HasValue)
            idx = Mathf.Clamp(s_lastIndex.Value, 0, list.Count - 1);

        if (idx < 0) idx = 0;

        var target = list[idx];
        if (!target || !target.IsActive() || !target.interactable) yield break;

        var es = EventSystem.current;
        if (!es) yield break;

        // Force a true selection change
        es.SetSelectedGameObject(null);
        yield return null; // let UI module process the deselect
        es.SetSelectedGameObject(target.gameObject);
        target.Select();
        // Debug.Log($"[VNOptionsFocusKeeper] Restored idx={idx}, label='{labels[idx]}'");
    }
}
