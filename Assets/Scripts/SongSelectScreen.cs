using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class SongSelectScreen : MonoBehaviour, IUIScreen
{
    [Header("Screen wiring")]
    [SerializeField] private GameObject root;              // Screen_Gallery panel
    [SerializeField] private GameObject firstSelected;     // default focus (e.g., Back button)
    [SerializeField] private ScreenController screens;
    [SerializeField] private Button backButton;

    public string ScreenId => MenuIds.SongSelect;            // adjust if you use a different id
    public GameObject Root => root != null ? root : gameObject;
    public GameObject FirstSelected => firstSelected;
    
    [Header("Data")]
    [SerializeField] SongDatabase database;
    public SongInfo Current => database[currentSong];
    
    [Header("Load Transition")]
    [SerializeField] string gameplaySceneName = "RhythmGamePlayScene";

    [Header("Decision / Fade / Scene")]
    [SerializeField] GameObject decisionOverlay;     // enable instantly
    [SerializeField] float fadeStartDelay   = 0.35f; // wait before starting fade
    [SerializeField] float fadeToBlackDuration = 0.45f;
    [SerializeField] float blackHoldSeconds = 2.0f;
    
    [Header("Left Panel (optional wiring)")]
    [SerializeField] Image bigJacket;
    [SerializeField] TextMeshProUGUI bpmText;
    [SerializeField] RadarGraph radar;
    [SerializeField] AudioSource preview;

    [Header("List")]
    [SerializeField] RectTransform listRoot;
    [SerializeField] SongTileView tilePrefab;
    [SerializeField, Min(3)] int visibleCount = 9;
    [SerializeField] float itemHeight = 100f;
    [SerializeField] float spacing    = 10f;
    [SerializeField] float moveDuration = 0.12f;
    [SerializeField] Ease  moveEase     = Ease.OutCubic;

    [Header("Selector (stationary)")]
    [SerializeField] RectTransform selector;

    [Header("Held-stick repeat")]
    [SerializeField] float repeatDelay = 0.35f;
    [SerializeField] float repeatRate  = 8.333333f; // ~10/sec
    
    // Track which tile is currently focused while the list is tweening.
    // We toggle only when this changes to avoid multiple tiles animating.
    SongTileView _tweenFocused;
    

    // ───────────────────────── internals ─────────────────────────
    readonly List<SongTileView> tiles = new List<SongTileView>();
    Tweener moveTween;
    bool busy;                  // while the list tween is running
    bool transitioning;         // after Confirm, lock inputs
    int queuedSteps;            // buffered steps while held
    int repeatDir;              // -1 up, +1 down, 0 idle
    float repeatTimer;

    int currentSong;            // logical index into database
    int centerIdx => visibleCount / 2;
    float RowStep => itemHeight + spacing;

    void Awake()
    {
        if (root == null) root = gameObject;
        if (backButton) backButton.onClick.AddListener(OnBack);
    }

    public void OnShow(object args)
    {
        // keep focus predictable when ScreenController shows this screen
        if (FirstSelected) EventSystem.current.SetSelectedGameObject(FirstSelected);

        // it’s fine if these also run in OnEnable the first time; calling here ensures
        // a clean refresh when returning to this screen via Pop/Push
        UpdateLeftPanel();
        ApplyFocusTween();
    }

    public void OnHide()
    {
        // lightweight cleanup
        if (preview) preview.Stop();
        moveTween?.Kill();
        busy = false;
        repeatDir = 0;
        queuedSteps = 0;
    }

    // ───────────────────────── existing logic ─────────────────────────
    void OnEnable()
    {
        BuildList();
        UpdateLeftPanel();
        FocusCenterTile();
        ApplyFocusTween();
    }

    // Build or rebuild the ring of tiles and bind data around currentSong
    void BuildList()
    {
        // clear existing
        for (int i = tiles.Count - 1; i >= 0; i--)
        {
            if (tiles[i]) Destroy(tiles[i].gameObject);
        }
        tiles.Clear();

        for (int i = 0; i < visibleCount; i++)
        {
            var t  = Instantiate(tilePrefab, listRoot);
            var rt = (RectTransform)t.transform;

            // Full-width row we position manually (no LayoutGroup)
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);

            // IMPORTANT: pivot at (1, 0.5) so the tile grows to the LEFT (IIDX feel)
            rt.pivot = new Vector2(1f, 0.5f);

            // Lock the row’s height so RowStep math is exact
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, itemHeight);

            // Place row i exactly: y = -i * (itemHeight + spacing)
            rt.anchoredPosition = new Vector2(0f, -i * RowStep);

            tiles.Add(t);
        }

        RebindAll();
        _tweenFocused = null; // reset any in-flight focus tracking
    }




    // Bind ring contents: center = currentSong, others wrap around DB
    void RebindAll()
    {
        if (database == null || database.Count == 0) return;

        for (int i = 0; i < tiles.Count; i++)
        {
            int delta = i - centerIdx;
            int idx = WrapIndex(currentSong + delta, database.Count);
            tiles[i].Bind(database[idx]);
            tiles[i].SetFocusWeight(i == centerIdx ? 1f : 0f); // instant
        }
    }

    static int WrapIndex(int i, int count)
    {
        if (count <= 0) return 0;
        i %= count;
        if (i < 0) i += count;
        return i;
    }

    // ───────────────────────── Input (Send Messages) ─────────────────────────
    // Called by PlayerInput (Behavior = Send Messages)
    public void OnNavigate(InputValue value)
    {
        if (transitioning) return;

        var v = value.Get<Vector2>();
        int dir = 0;
        if (v.y > +0.5f) dir = -1; // up
        if (v.y < -0.5f) dir = +1; // down

        if (dir == 0)
        {
            // stick centered: stop repeating and clear buffer
            repeatDir = 0;
            queuedSteps = 0;
            return;
        }

        // First press or changed direction: move immediately and arm repeat
        if (!busy || repeatDir != dir)
        {
            repeatDir = dir;
            repeatTimer = repeatDelay;
            Move(dir);
            return;
        }

        // Holding same direction while tween busy -> queue
        if (busy && repeatDir == dir)
            queuedSteps++;
    }

    void Update()
    {
        if (transitioning || repeatDir == 0) return;

        // Handle held repeat on real time (independent of timescale)
        repeatTimer -= Time.unscaledDeltaTime;
        if (repeatTimer <= 0f)
        {
            if (!busy)
            {
                Move(repeatDir);
                repeatTimer = 1f / Mathf.Max(0.01f, repeatRate);
            }
        }
    }

    public void OnSubmit(InputValue value)
    {
        if (value.isPressed) Confirm();
    }

    public void OnCancel(InputValue value)
    {
        if (value.isPressed) OnBack();
    }

    // ───────────────────────── Movement ─────────────────────────
    void Move(int dir)
    {
        if (busy || dir == 0) return;

        busy = true;
        _tweenFocused = null; 
        moveTween?.Kill();

        // tween list root by exactly one row
        var from = listRoot.anchoredPosition;
        var to   = from + new Vector2(0f, -dir * RowStep);

        moveTween = listRoot
            .DOAnchorPos(to, moveDuration)
            .SetEase(moveEase)
            .SetUpdate(true)
            .OnUpdate(UpdateFocusDynamic)
            .OnComplete(() =>
            {
                // lock the root back to the canonical position,
                // rotate the visual ring and rebind data
                listRoot.anchoredPosition = from;
                RotateTilesAndRebind(dir);

                // logical selection moved
                currentSong = WrapIndex(currentSong + dir, database.Count);
                UpdateLeftPanel();

                // snap one tile focused
                ApplyFocusTween();
                FocusCenterTile();

                busy = false;

                // drain queued steps for held input
                if (queuedSteps > 0 && repeatDir != 0)
                {
                    queuedSteps--;
                    Move(repeatDir);
                }
            })
            .SetLink(listRoot.gameObject, LinkBehaviour.KillOnDestroy);
    }

    // During tween: weight only the tile closest to the selector
    void UpdateFocusDynamic()
    {
        if (selector == null) { ResetFocusInstant(); return; }

        // Find the single tile closest to the selector’s center
        float selY = selector.TransformPoint(selector.rect.center).y;

        SongTileView best = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < tiles.Count; i++)
        {
            var rt = (RectTransform)tiles[i].transform;
            float y = rt.TransformPoint(rt.rect.center).y;
            float d = Mathf.Abs(y - selY);
            if (d < bestDist) { bestDist = d; best = tiles[i]; }
        }

        // If the focused tile changed during the tween, toggle the two tiles.
        if (best != _tweenFocused)
        {
            if (_tweenFocused != null) _tweenFocused.SetFocused(false);
            if (best != null) best.SetFocused(true);
            _tweenFocused = best;
        }
    }

    void ResetFocusInstant()
    {
        for (int i = 0; i < tiles.Count; i++)
            tiles[i].SetFocusWeight(i == centerIdx ? 1f : 0f);
    }

    void ApplyFocusTween()
    {
        var under = TileUnderSelector();
        for (int i = 0; i < tiles.Count; i++)
            tiles[i].SetFocused(tiles[i] == under);
    }

    SongTileView TileUnderSelector()
    {
        if (selector == null) return tiles[centerIdx];

        float selY = selector.TransformPoint(selector.rect.center).y;

        SongTileView best = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < tiles.Count; i++)
        {
            var rt = (RectTransform)tiles[i].transform;
            float y = rt.TransformPoint(rt.rect.center).y;
            float d = Mathf.Abs(y - selY);
            if (d < bestDist) { bestDist = d; best = tiles[i]; }
        }
        return best ?? tiles[centerIdx];
    }

    void RotateTilesAndRebind(int dir)
    {
        if (dir > 0) // moved down: list visually went up, bring top tile to bottom
        {
            var first = tiles[0];
            tiles.RemoveAt(0);
            tiles.Add(first);
        }
        else // moved up: bring bottom tile to top
        {
            var last = tiles[tiles.Count - 1];
            tiles.RemoveAt(tiles.Count - 1);
            tiles.Insert(0, last);
        }

        // put them back into canonical slots
        for (int i = 0; i < tiles.Count; i++)
        {
            var rt = (RectTransform)tiles[i].transform;
            rt.anchoredPosition = new Vector2(0f, -i * RowStep);
        }

        RebindAll();
    }

    void FocusCenterTile()
    {
        if (tiles == null || tiles.Count <= centerIdx) return;

        var center = tiles[centerIdx];
        var toSelect = center.Button ? center.Button.gameObject : center.gameObject;

        firstSelected = toSelect; // keep ScreenController UI happy
        EventSystem.current.SetSelectedGameObject(toSelect);
    }

    // ───────────────────────── Confirm / Back ─────────────────────────
    void Confirm()
    {
        if (transitioning) return;
        _ = CoConfirmAsync(); // fire & forget (we guard with transitioning flag)
    }

    async Task CoConfirmAsync()
    {
        transitioning = true;

        // stop preview & swallow inputs
        if (preview) preview.Stop();
        moveTween?.Kill();
        busy = false;
        repeatDir = 0;
        queuedSteps = 0;

        // Show decision overlay immediately
        if (decisionOverlay) decisionOverlay.SetActive(true);

        // Optional delay before the fade starts
        if (fadeStartDelay > 0f)
            await Task.Delay(TimeSpan.FromSeconds(fadeStartDelay));

        // Force the global fade overlay active and fade to black
        if (Fade.Instance != null)
        {
            var go = Fade.Instance.gameObject;
            if (!go.activeInHierarchy) go.SetActive(true);
            await Fade.Instance.Out(fadeToBlackDuration); // async Task API in your Fade class
        }

        // Hold on black
        if (blackHoldSeconds > 0f)
            await Task.Delay(TimeSpan.FromSeconds(blackHoldSeconds));

        // Load gameplay
        if (!string.IsNullOrEmpty(gameplaySceneName))
            SceneManager.LoadScene(gameplaySceneName);
    }

    public void OnBack()
    {
        if (transitioning) return;
        if (preview) preview.Stop();
        screens?.Pop();
    }

    // ───────────────────────── Left panel helpers ─────────────────────────
    void UpdateLeftPanel()
    {
        var s = Current;
        if (s == null) return;

        if (bigJacket) bigJacket.sprite = s.jacket;
        if (bpmText)   bpmText.text    = $"BPM {s.bpm}";
        if (radar)     radar.SetValues(s.stream, s.voltage, s.freeze, s.chaos, s.air);   // RadarGraph API
        if (preview)
        {
            preview.clip = s.previewClip;
            preview.Play();
        }
    }
}
