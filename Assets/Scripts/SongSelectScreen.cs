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

    [Header("Preview Timing")]
    [SerializeField, Tooltip("Delay before starting a new preview after selection stops moving.")]
    float previewDelay = 0.25f;
    [SerializeField, Tooltip("Fade-in duration for the preview volume.")]
    float previewFadeIn = 0.12f;

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

    // ───────── preview debounce state ─────────
    int _previewTicket;                 // cancels pending coroutines on change
    Coroutine _previewDelayCR;
    Coroutine _previewLoopCR;
    

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

        if (preview) preview.playOnAwake = false;
    }

    public void OnShow(object args)
    {
        if (FirstSelected) EventSystem.current.SetSelectedGameObject(FirstSelected);
        UpdateLeftPanel();      // updates art/text
        ApplyFocusTween();

        // schedule the initial preview with a short delay (feels nicer)
        SchedulePreview(Current);
    }

    public void OnHide()
    {
        CancelPreview();
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

        SchedulePreview(Current);
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

        // as soon as we start moving, cancel any pending/playing preview
        CancelPreview();

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

                // schedule preview for the newly selected tile (debounced)
                SchedulePreview(Current);

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
        if (!selector) { ResetFocusInstant(); return; }

        float selY = selector.TransformPoint(selector.rect.center).y;

        for (int i = 0; i < tiles.Count; i++)
        {
            var  rt = (RectTransform)tiles[i].transform;
            float y = rt.TransformPoint(rt.rect.center).y;
            float d = Mathf.Abs(y - selY);

            // 1 at selector, fading to 0 over ~one row
            float w = Mathf.Clamp01(1f - (d / RowStep));
            tiles[i].SetFocusWeight(w);
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
        {
            bool isCenter = (tiles[i] == under);
            tiles[i].SetFocused(isCenter, instant: true);   // instant snap, no extra tween
        }
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
        CancelPreview();

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
        CancelPreview();
        screens?.Pop();
    }

    // ───────────────────────── Left panel helpers ─────────────────────────
    void UpdateLeftPanel()
    {
        var s = Current;
        if (s == null) return;

        if (bigJacket) bigJacket.sprite = s.jacket;
        if (bpmText)   bpmText.text    = $"BPM {s.bpm}";
        if (radar)     radar.SetValues(s.stream, s.voltage, s.freeze, s.chaos, s.air);
    }

    // ───────── preview control (debounced + loop + fade-in) ─────────
    void SchedulePreview(SongInfo s)
    {
        if (!preview) return;

        // cancel previous schedule/loop and stop sound
        CancelPreview();

        // no clip? nothing to do
        if (s == null || s.previewClip == null) return;

        _previewDelayCR = StartCoroutine(CoPreviewAfterDelay(s, ++_previewTicket));
    }

    System.Collections.IEnumerator CoPreviewAfterDelay(SongInfo s, int ticket)
    {
        // small delay to avoid blasting while scrolling quickly
        float t = 0f;
        while (t < previewDelay)
        {
            if (ticket != _previewTicket) yield break; // canceled/replaced
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (ticket != _previewTicket) yield break;
        if (!preview) yield break;

        // set up and play quietly, then fade in
        preview.clip   = s.previewClip;
        preview.time   = Mathf.Max(0f, s.previewStart);
        preview.volume = 0f;
        preview.Play();

        // start loop watcher
        _previewLoopCR = StartCoroutine(CoPreviewLoop(s, ticket));

        // fade in
        float dur = Mathf.Max(0f, previewFadeIn);
        float v = 0f;
        while (v < 1f && dur > 0f)
        {
            if (ticket != _previewTicket) yield break;
            v += Time.unscaledDeltaTime / dur;
            preview.volume = Mathf.Clamp01(v);
            yield return null;
        }
        preview.volume = 1f;
    }

    System.Collections.IEnumerator CoPreviewLoop(SongInfo s, int ticket)
    {
        // Loop between previewStart/previewEnd (or to previewLoop) if provided
        while (preview && preview.isPlaying && ticket == _previewTicket)
        {
            if (s.previewEnd > 0f && preview.time >= s.previewEnd)
            {
                float loopPoint = (s.previewLoop >= 0f ? s.previewLoop : s.previewStart);
                preview.time = Mathf.Max(0f, loopPoint);
            }
            yield return null;
        }
    }

    void CancelPreview()
    {
        // invalidate tickets so any pending coroutines exit
        _previewTicket++;

        if (_previewDelayCR != null) StopCoroutine(_previewDelayCR);
        if (_previewLoopCR  != null) StopCoroutine(_previewLoopCR);
        _previewDelayCR = _previewLoopCR = null;

        if (preview)
        {
            preview.Stop();
            preview.volume = 1f; // reset for next start
        }
    }
}
