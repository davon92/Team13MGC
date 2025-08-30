using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class GalleryTile : MonoBehaviour, ISelectHandler, IPointerEnterHandler
{
    [Header("Wiring")]
    [SerializeField] Image       thumb;
    [SerializeField] GameObject  lockOverlay;
    [SerializeField] Button      button;

    [Header("Locked Visuals")]
    [Tooltip("Optional placeholder used when the item is locked. If empty, the thumbnail is hidden completely.")]
    [SerializeField] Sprite lockedPlaceholder;

    GalleryItem item;
    Action<GalleryItem>  onClicked;
    Action<RectTransform> onSelected;
    bool isUnlocked;
    RectTransform rt;

    void Awake() => rt = transform as RectTransform;

    public void Setup(GalleryItem item, bool unlocked, Action<GalleryItem> onClicked)
    {
        this.item      = item;
        this.onClicked = onClicked;
        isUnlocked     = unlocked;

        // If locked, do NOT show the real thumbnail underneath the overlay.
        if (thumb)
        {
            if (unlocked)
            {
                thumb.enabled = true;
                thumb.sprite  = item.thumbnail;
            }
            else
            {
                if (lockedPlaceholder)
                {
                    thumb.enabled = true;
                    thumb.sprite  = lockedPlaceholder;
                }
                else
                {
                    // hide the real image entirely – only the lock overlay is visible
                    thumb.enabled = false;
                }
            }
        }

        if (lockOverlay) lockOverlay.SetActive(!unlocked);

        // Keep focusable for navigation, but only fire click when unlocked.
        if (button)
        {
            button.interactable = true; // allow highlight & pad navigation on locked entries
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => { if (isUnlocked) onClicked?.Invoke(item); });
        }
    }

    // Allows GalleryScreen to get a callback when selection changes.
    public void SetSelectionForwarder(Action<RectTransform> callback) => onSelected = callback;

    // Fired by the EventSystem when this control becomes the selected object.
    public void OnSelect(BaseEventData _) => onSelected?.Invoke(rt);

    // Optional: hovering with the mouse also moves selection
    public void OnPointerEnter(PointerEventData _) =>
        EventSystem.current?.SetSelectedGameObject(button ? button.gameObject : gameObject);
}
