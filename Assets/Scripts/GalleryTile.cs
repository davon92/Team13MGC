using System;
using UnityEngine;
using UnityEngine.UI;

public class GalleryTile : MonoBehaviour
{
    [SerializeField] Image thumb;
    [SerializeField] GameObject lockOverlay;
    [SerializeField] Button button;

    GalleryItem item;
    Action<GalleryItem> onClicked;
    bool isUnlocked;

    public void Setup(GalleryItem item, bool unlocked, Action<GalleryItem> onClicked)
    {
        this.item = item;
        this.onClicked = onClicked;
        this.isUnlocked = unlocked;

        if (thumb) thumb.sprite = item.thumbnail;
        if (lockOverlay) lockOverlay.SetActive(!unlocked);

        if (button)
        {
            // Keep it selectable/highlightable for navigation.
            button.interactable = true;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnPressed);
        }
    }

    void OnPressed()
    {
        if (isUnlocked)
        {
            onClicked?.Invoke(item);
        }
        else
        {
            // Optional: tiny feedback for "locked"
            // (e.g., flash the overlay or play a sound)
            // StartCoroutine(FlashLocked());
        }
    }
}