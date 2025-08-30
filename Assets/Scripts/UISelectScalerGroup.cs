using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class UISelectScalerGroup : MonoBehaviour
{
    [SerializeField] private Transform scopeRoot;
    [SerializeField] private bool syncOnEnable = true;
    [SerializeField] private bool keepLastWhenOutside = true;   // ← ensure true
    [SerializeField] private bool pollSelection = true;
    [SerializeField] private bool instantOnSync = true;

    private readonly List<ScaleOnSelect> _items = new();
    private GameObject _lastSelected;
    private ScaleOnSelect _current;

    public void SetKeepLastWhenOutside(bool on) => keepLastWhenOutside = on;

    void Reset() => scopeRoot = transform;

    void OnEnable()
    {
        if (!scopeRoot) scopeRoot = transform;
        Collect();
        if (syncOnEnable) SyncNow(instantOnSync);
    }

    void Update()
    {
        if (!pollSelection || EventSystem.current == null) return;
        var sel = EventSystem.current.currentSelectedGameObject;
        if (sel == _lastSelected) return;
        _lastSelected = sel;
        SyncNow(false);
    }

    void Collect()
    {
        _items.Clear();
        if (!scopeRoot) scopeRoot = transform;
        scopeRoot.GetComponentsInChildren(true, _items);
    }

    public void SyncNow(bool instant = false)
    {
        if (_items.Count == 0) Collect();

        var sel = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
        var target = FindOwner(sel);

        if (target == null)
        {
            if (keepLastWhenOutside && _current != null)
            {
                // Actively keep the underline visible on the last tab
                _current.SetSelected(true, instant);
                return;
            }
            foreach (var it in _items) if (it) it.SetSelected(false, instant);
            _current = null;
            return;
        }

        if (_current == target) return;

        foreach (var it in _items) if (it) it.SetSelected(it == target, instant);
        _current = target;
    }

    // Force the underline to a given tab (used on select/click)
    public void ForceSelect(Transform tabTransform, bool instant = true)
    {
        if (!tabTransform) return;
        var target = tabTransform.GetComponentInParent<ScaleOnSelect>(true);
        if (!target) target = FindOwner(tabTransform.gameObject);
        if (!target) return;

        foreach (var it in _items) if (it) it.SetSelected(it == target, instant);
        _current = target;
    }

    public bool IsOwner(GameObject go) => FindOwner(go) != null;

    private ScaleOnSelect FindOwner(GameObject selected)
    {
        if (!selected) return null;
        var t = selected.transform;
        foreach (var it in _items) if (it && t.IsChildOf(it.transform)) return it;
        return null;
    }
}
