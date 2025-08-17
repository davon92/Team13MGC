using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class GalleryTile : MonoBehaviour, ISelectHandler, IPointerEnterHandler
{
    [SerializeField] Image thumb;
    [SerializeField] GameObject lockOverlay;
    [SerializeField] Button button;

    GalleryItem item;
    Action<GalleryItem> onClicked;
    Action<RectTransform> onSelected;
    bool isUnlocked;
    RectTransform rt;

    void Awake() => rt = transform as RectTransform;

    public void Setup(GalleryItem item, bool unlocked, Action<GalleryItem> onClicked)
    {
        this.item = item;
        this.onClicked = onClicked;
        isUnlocked = unlocked;

        if (thumb) thumb.sprite = item.thumbnail;
        if (lockOverlay) lockOverlay.SetActive(!unlocked);

        // Keep it focusable for pad/keyboard navigation even when locked.
        if (button)
        {
            button.interactable = true;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => { if (isUnlocked) onClicked?.Invoke(item); });
        }
    }

    // GalleryScreen calls this once so we can notify it when we’re selected.
    public void SetSelectionForwarder(Action<RectTransform> callback) => onSelected = callback;

    // Fired by the EventSystem when this control becomes the selected object.
    public void OnSelect(BaseEventData _) => onSelected?.Invoke(rt);

    // Optional: hovering with the mouse moves selection to this tile too.
    public void OnPointerEnter(PointerEventData _) =>
        EventSystem.current?.SetSelectedGameObject(button ? button.gameObject : gameObject);
}