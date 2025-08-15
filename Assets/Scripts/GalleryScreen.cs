using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class GalleryScreen : MonoBehaviour, IUIScreen
{
    
    [Header("Screen wiring")]
    [SerializeField] private GameObject root;              // Screen_Gallery panel
    [SerializeField] private GameObject firstSelected;     // default focus (e.g., Back button)
    [SerializeField] private ScreenController screens;
    [SerializeField] private Button backButton;

    public string ScreenId => MenuIds.Gallery;            // adjust if you use a different id
    public GameObject Root => root != null ? root : gameObject;
    public GameObject FirstSelected => firstSelected;

    [Header("Data")]
    [SerializeField] private GalleryDatabase database;

    [Header("UI")]
    [SerializeField] private Transform gridRoot;     // ScrollRect/Content
    [SerializeField] private GalleryTile tilePrefab;
    [SerializeField] private ScrollRect scroll;
    [SerializeField] private FullscreenImageViewer viewer;

    readonly List<GameObject> pool = new();
    GameObject lastTileFocused;                       // remember a sensible focus after closing viewer

    void Awake()
    {
        if (root == null) root = gameObject;
        if (backButton) backButton.onClick.AddListener(OnBack);
    }

    // IUIScreen
    public void OnShow(object args)
    {
        BuildGrid();
        viewer?.Hide();

        // pick initial focus: a tile if we found one, otherwise whatever you set in the inspector
        if (firstSelected)
            EventSystem.current.SetSelectedGameObject(firstSelected);
    }

    public void OnHide() => viewer?.Hide();

    void BuildGrid()
    {
        // Clear
        foreach (var go in pool) Destroy(go);
        pool.Clear();

        if (!database || !gridRoot || !tilePrefab) return;

        Button firstTileBtn = null;
        Button firstUnlockedBtn = null;

        foreach (var item in database.items)
        {
            bool unlocked = GallerySaves.IsUnlocked(item.id);
            if (!unlocked && item.hideIfLocked) continue;

            var tile = Instantiate(tilePrefab, gridRoot);
            tile.Setup(item, unlocked, OnTileClicked);
            pool.Add(tile.gameObject);

            if (firstTileBtn == null) firstTileBtn = tile.GetComponent<Button>();
            if (unlocked && firstUnlockedBtn == null) firstUnlockedBtn = tile.GetComponent<Button>();
        }
        
        var toFocus = firstTileBtn?.gameObject;

        if (toFocus != null)
        {
            firstSelected = toFocus;
            EventSystem.current.SetSelectedGameObject(firstSelected);
        }

    }
    
    void ScrollTo(RectTransform tile)
    {
        if (!scroll || !tile) return;
        Canvas.ForceUpdateCanvases();                   // make sure layouts are up-to-date
        var content = scroll.content;
        var view = scroll.viewport;

        // move content so that tile’s left edge is near the viewport’s left
        Vector3 worldPoint = tile.TransformPoint(new Vector3(tile.rect.xMin, 0, 0));
        Vector3 localPoint = view.InverseTransformPoint(worldPoint);
        float offset = (view.rect.xMin - localPoint.x);
        content.anchoredPosition += new Vector2(offset, 0);
    }

    void OnTileClicked(GalleryItem item)
    {
        lastTileFocused = EventSystem.current.currentSelectedGameObject;
        viewer.Show(item);
        EventSystem.current.SetSelectedGameObject(null); // freeze focus while viewer is open
    }

    // --- Back / Cancel ---
    public void OnBack()
    {
        if (viewer && viewer.IsOpen)
        {
            viewer.Hide();
            EventSystem.current.SetSelectedGameObject(lastTileFocused ? lastTileFocused : firstSelected);
            return;
        }
        screens?.Pop();
    }
    

    public void OnCancel(BaseEventData _) => OnBack();
    
    [ContextMenu("Unlock All Gallery (and Rebuild)")]
    void UnlockAllAndRebuild()
    {
        if (!database) return;
        foreach (var it in database.items)
            GallerySaves.Unlock(it.id);   // writes to PlayerPrefs
        BuildGrid();                       // forces UI to refresh
    }

    [ContextMenu("Clear Gallery Unlocks (and Rebuild)")]
    void ClearUnlocksAndRebuild()
    {
        GallerySaves.ClearAll();           // wipes PlayerPrefs for gallery
        BuildGrid();
    }
}
