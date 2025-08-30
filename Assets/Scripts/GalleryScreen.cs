using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class GalleryScreen : MonoBehaviour, IUIScreen
{
    [Header("Screen wiring")]
    [SerializeField] private GameObject root;              // Screen_Gallery panel
    [SerializeField] private GameObject firstSelected;     // will be replaced by first tile
    [SerializeField] private ScreenController screens;
    [SerializeField] private Button backButton;

    public string     ScreenId      => MenuIds.Gallery;
    public GameObject Root          => root != null ? root : gameObject;
    public GameObject FirstSelected => firstSelected;

    [Header("Data")]
    [SerializeField] private GalleryDatabase database;

    [Header("UI")]
    [SerializeField] private Transform   gridRoot;     // ScrollRect/Content
    [SerializeField] private GalleryTile tilePrefab;
    [SerializeField] private ScrollRect  scroll;
    [SerializeField] private FullscreenImageViewer viewer;

    readonly List<GameObject> pool = new();
    GameObject lastTileFocused;

    void Awake()
    {
        if (root == null) root = gameObject;
        if (backButton) backButton.onClick.AddListener(OnBack);
    }

    // IUIScreen
    public void OnShow(object args)
    {
        BuildGrid();

        // Ensure we start at the top of the list, not the middle.
        if (scroll)
        {
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 1f;   // 1 = top, 0 = bottom
        }

        viewer?.Hide();

        // Select on the *next* frame so layout/ScrollRect are settled.
        if (firstSelected) StartCoroutine(SelectNextFrame(firstSelected));
    }

    public void OnHide()
    {
        viewer?.Hide();
    }

    void BuildGrid()
    {
        // Clear old tiles
        foreach (var go in pool) Destroy(go);
        pool.Clear();

        if (!database || !gridRoot || !tilePrefab) return;

        Button firstTileBtn = null;
        Button firstUnlockedBtn = null;

        foreach (var item in database.items)
        {
            bool unlocked = GallerySaves.IsUnlocked(item.id); // reads PlayerPrefs once and caches  :contentReference[oaicite:0]{index=0}
            if (!unlocked && item.hideIfLocked) continue;     // optional hide rule on item         :contentReference[oaicite:1]{index=1}

            var tile = Instantiate(tilePrefab, gridRoot);
            tile.Setup(item, unlocked, OnTileClicked);
            tile.SetSelectionForwarder(ScrollTo);
            pool.Add(tile.gameObject);

            var btn = tile.GetComponent<Button>();
            if (!btn) continue;

            if (firstTileBtn == null) firstTileBtn = btn;
            if (unlocked && firstUnlockedBtn == null) firstUnlockedBtn = btn;
        }

        // Prefer the first *unlocked* tile for focus; otherwise the very first tile.
        var prefer = (firstUnlockedBtn ? firstUnlockedBtn : firstTileBtn);
        firstSelected = prefer ? prefer.gameObject : firstSelected;
    }

    System.Collections.IEnumerator SelectNextFrame(GameObject go)
    {
        // one frame for layout & ScrollRect to settle
        yield return null;
        if (!go) yield break;

        EventSystem.current.SetSelectedGameObject(go);  // selection triggers GalleryTile.OnSelect which calls ScrollTo  :contentReference[oaicite:2]{index=2}
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

        // Convert corners into viewport-local space
        Vector3[] v = new Vector3[4]; view.GetWorldCorners(v);
        Vector3[] t = new Vector3[4]; tile.GetWorldCorners(t);
        for (int i = 0; i < 4; i++)
        {
            v[i] = view.InverseTransformPoint(v[i]);
            t[i] = view.InverseTransformPoint(t[i]);
        }

        // Positive dy -> tile above view (need to scroll down); Negative -> below view (scroll up).
        float dy = 0f;
        if (t[1].y > v[1].y)      dy = t[1].y - v[1].y; // top above
        else if (t[0].y < v[0].y) dy = t[0].y - v[0].y; // bottom below

        if (Mathf.Abs(dy) < 0.5f) return; // already fully visible

        float deltaNorm = dy / scrollable;
        scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition + deltaNorm);
    }

    void OnTileClicked(GalleryItem item)
    {
        lastTileFocused = EventSystem.current.currentSelectedGameObject;
        viewer?.Show(item);

        // keep a valid selection so Cancel (B/Esc) still returns to the grid
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

    public void OnUI_Cancel(InputValue _) => OnBack();

    // QA helpers
    [ContextMenu("Unlock All Gallery (and Rebuild)")]
    void UnlockAllAndRebuild()
    {
        if (!database) return;
        foreach (var it in database.items) GallerySaves.Unlock(it.id); // writes PlayerPrefs  :contentReference[oaicite:3]{index=3}
        BuildGrid();
        if (scroll) { Canvas.ForceUpdateCanvases(); scroll.verticalNormalizedPosition = 1f; }
        if (firstSelected) StartCoroutine(SelectNextFrame(firstSelected));
    }

    [ContextMenu("Clear Gallery Unlocks (and Rebuild)")]
    void ClearUnlocksAndRebuild()
    {
        GallerySaves.ClearAll();  // clears PlayerPrefs                                   :contentReference[oaicite:4]{index=4}
        BuildGrid();
        if (scroll) { Canvas.ForceUpdateCanvases(); scroll.verticalNormalizedPosition = 1f; }
        if (firstSelected) StartCoroutine(SelectNextFrame(firstSelected));
    }
}
