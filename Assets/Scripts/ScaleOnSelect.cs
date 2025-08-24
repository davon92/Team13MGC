using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;

/// Put this on the Button (Selectable) that owns the highlight.
/// Assign 'graphic' to the underline (or whatever you want to scale).
public class ScaleOnSelect : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] Transform graphic;          // underline / highlight
    [SerializeField] float selectedScale = 1f;   // absolute scale (on the chosen axis)
    [SerializeField] float duration = 0.12f;
    [SerializeField] Ease ease = Ease.OutQuad;
    [SerializeField] Axis axis = Axis.X;         // usually X for underline
    [SerializeField] bool setZeroOnEnable = true;
    [SerializeField] bool hoverActsLikeSelect = true; // if you want mouse hover to mirror selection

    public enum Axis { X, Y, XY }

    Vector3 _base; // captured natural scale

    void Reset()
    {
        if (!graphic) graphic = transform;
    }

    void OnEnable()
    {
        if (!graphic) return;
        graphic.DOKill();
        _base = graphic.localScale;
        if (setZeroOnEnable) Apply(false, instant: true);
    }

    void OnDisable()
    {
        if (!graphic) return;
        graphic.DOKill();
    }

    // === Public API (used by the group helper) ===
    public void SetSelected(bool selected, bool instant = false) => Apply(selected, instant);

    // === EventSystem hooks ===
    public void OnSelect(BaseEventData _)  => Apply(true,  false);
    public void OnDeselect(BaseEventData _) => Apply(false, false);

    public void OnPointerEnter(PointerEventData _)
    {
        if (!hoverActsLikeSelect) return;

        // Make this the selected object; OnSelect will drive the grow animation.
        if (EventSystem.current &&
            EventSystem.current.currentSelectedGameObject != gameObject)
        {
            EventSystem.current.SetSelectedGameObject(gameObject);
        }
    }

    public void OnPointerExit(PointerEventData _)
    {
        if (!hoverActsLikeSelect) return;
        // Do nothing. If this is still selected, keep it grown.
        // When selection actually moves elsewhere, OnDeselect will shrink it.
    }

    // === Core ===
    void Apply(bool selected, bool instant)
    {
        if (!graphic) return;
        graphic.DOKill();

        // Always keep "other" axes at 1 so the bar has thickness.
        // Only the chosen axis animates between 0 and selectedScale.
        float sx = 1f, sy = 1f, sz = 1f;

        switch (axis)
        {
            case Axis.X:
                sx = selected ? selectedScale : 0f; // grow/shrink width
                // keep sy = 1 so it's not flattened
                break;

            case Axis.Y:
                sy = selected ? selectedScale : 0f; // grow/shrink height
                // keep sx = 1 so it's not flattened
                break;

            case Axis.XY:
                sx = selected ? selectedScale : 0f;
                sy = selected ? selectedScale : 0f;
                break;
        }

        var target = new Vector3(sx, sy, sz);
        if (instant) graphic.localScale = target;
        else graphic.DOScale(target, duration).SetEase(ease).SetUpdate(true);
    }

}
