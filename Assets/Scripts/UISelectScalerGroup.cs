using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// Keeps a group of ScaleOnSelect in sync with the current UI selection.
/// - Shrinks all non-selected underlines to 0
/// - Grows only the selected one
/// - Can snap instantly on enable/show to avoid any flicker
public class UISelectScalerGroup : MonoBehaviour
{
    [Tooltip("Scope for finding ScaleOnSelect items; defaults to this transform.")]
    [SerializeField] private Transform scopeRoot;

    [Tooltip("Snap all visuals to the correct state on enable.")]
    [SerializeField] private bool syncOnEnable = true;

    [Tooltip("Continuously watch selection changes each frame.")]
    [SerializeField] private bool pollSelection = true;

    [Tooltip("When syncing on enable or explicit call, apply instantly (no tween).")]
    [SerializeField] private bool instantOnSync = true;

    private readonly List<ScaleOnSelect> _items = new();
    private GameObject _lastSelectedGO;
    private ScaleOnSelect _current;

    void Awake()
    {
        if (!scopeRoot) scopeRoot = transform;
        Collect();
    }

    void OnEnable()
    {
        if (syncOnEnable) SyncNow(instant: instantOnSync);
    }

    void Update()
    {
        if (!pollSelection) return;
        var sel = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
        if (sel == _lastSelectedGO) return;
        _lastSelectedGO = sel;
        SyncNow(instant: false);
    }

    public void Collect()
    {
        _items.Clear();
        scopeRoot.GetComponentsInChildren(true, _items);
    }

    /// Call this after you've set selection in OnShow (or anytime you want).
    public void SyncNow(bool instant = false)
    {
        if (_items.Count == 0) Collect();

        var sel = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
        var target = FindOwner(sel);

        // If selection isn't under this group, shrink all.
        if (target == null)
        {
            foreach (var it in _items) if (it) it.SetSelected(false, instant);
            _current = null;
            return;
        }

        if (_current == target) return; // nothing to change

        // Update visuals: only 'target' selected
        foreach (var it in _items)
        {
            if (!it) continue;
            it.SetSelected(it == target, instant);
        }
        _current = target;
    }

    // Find the ScaleOnSelect that owns 'selected' (selected can be a child of the button)
    private ScaleOnSelect FindOwner(GameObject selected)
    {
        if (selected == null) return null;
        var t = selected.transform;
        foreach (var it in _items)
        {
            if (it && t.IsChildOf(it.transform)) return it;
        }
        return null;
    }
}
