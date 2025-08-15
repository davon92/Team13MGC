// Assets/Scripts/Gallery/FullscreenImageViewer.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FullscreenImageViewer : MonoBehaviour
{
    [SerializeField] GameObject root;
    [SerializeField] Image fullImage;
    [SerializeField] TMP_Text titleLabel;
    [SerializeField] TMP_Text captionLabel;

    public bool IsOpen => root && root.activeSelf;

    public void Show(GalleryItem item)
    {
        if (!root) return;
        root.SetActive(true);
        if (fullImage) fullImage.sprite = item.fullImage;
        if (titleLabel) titleLabel.text = item.title;
        if (captionLabel) captionLabel.text = item.caption;
        // TODO: optional fade, input capture, etc.
    }

    public void Hide()
    {
        if (root) root.SetActive(false);
    }
}