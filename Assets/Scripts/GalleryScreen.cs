using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

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
            tile.SetSelectionForwarder(ScrollTo);
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

        // Make sure layout numbers are current.
        Canvas.ForceUpdateCanvases();

        var view    = scroll.viewport;
        var content = scroll.content;

        // If there's nothing to scroll, bail.
        float scrollable = Mathf.Max(0f, content.rect.height - view.rect.height);
        if (scrollable <= 0f) return;

        // World corners -> viewport local space.
        Vector3[] v = new Vector3[4]; view.GetWorldCorners(v);
        Vector3[] t = new Vector3[4]; tile.GetWorldCorners(t);
        for (int i = 0; i < 4; i++)
        {
            v[i] = view.InverseTransformPoint(v[i]);
            t[i] = view.InverseTransformPoint(t[i]);
        }

        // dy > 0 means the tile's top is above the viewport's top (needs to scroll DOWN).
        // dy < 0 means the tile's bottom is below the viewport's bottom (needs to scroll UP).
        float dy = 0f;
        if (t[1].y > v[1].y)            // tile top above view top
            dy = t[1].y - v[1].y;
        else if (t[0].y < v[0].y)       // tile bottom below view bottom
            dy = t[0].y - v[0].y;

        if (Mathf.Abs(dy) < 0.5f) return; // already fully visible

        // Convert pixel delta (dy) into a normalized delta for ScrollRect.
        // verticalNormalizedPosition: 1 = top, 0 = bottom.
        float deltaNorm = dy / scrollable;
        scroll.verticalNormalizedPosition = Mathf.Clamp01(
            scroll.verticalNormalizedPosition + deltaNorm
        );
    }


    void OnTileClicked(GalleryItem item)
    {
        lastTileFocused = EventSystem.current.currentSelectedGameObject;
        viewer.Show(item);

        // keep a valid selection alive so Cancel (B/Esc) still routes to this screen
        var fallback = lastTileFocused ? lastTileFocused : firstSelected;
        if (fallback) EventSystem.current.SetSelectedGameObject(fallback);
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
    

    public void OnUI_Cancel(InputValue value) => OnBack();
    
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
