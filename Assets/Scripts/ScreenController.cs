using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public interface IUIScreen
{
    string     ScreenId       { get; }
    GameObject Root           { get; }
    GameObject FirstSelected  { get; }
    void OnShow(object args);
    void OnHide();
}

public class ScreenController : MonoBehaviour
{
    [Header("Screens (drag each screen script here)")]
    [SerializeField] private List<MonoBehaviour> screenBehaviours = new();
    [SerializeField] private string initialScreenId = "";
    [SerializeField] private bool autoShowOnStart = true;

    [Header("Transition")]
    [SerializeField] private bool fadeOnSwitch = true;
    [SerializeField, Range(0f, 1f)] private float fadeDuration = 0.20f;
    [SerializeField] private Ease easeIn  = Ease.OutQuad;
    [SerializeField] private Ease easeOut = Ease.InQuad;

    [Header("Behavior")]
    [Tooltip("When pushing a new screen, hide and disable the previous one so its Selectables are not part of navigation.")]
    [SerializeField] private bool pushHidesPrevious = true;

    public Stack<IUIScreen> stack = new();
    private Dictionary<string, IUIScreen> map;
    private bool _transitioning;

    public bool   CanPop => stack != null && stack.Count > 0;
    public string TopId  => stack != null && stack.Count > 0 ? stack.Peek().ScreenId : null;
    public bool   IsOnTop(string screenId) => TopId == screenId;
    public string InitialScreenId => initialScreenId;
    public bool   AutoShowOnStart => autoShowOnStart;

    // Remember last selected (GO) per screen (same-scene restores)
    private readonly Dictionary<string, GameObject> _lastSelectedByScreen = new();
    // NEW: explicitly remember the item that initiated a Push so Pop returns to it
    private readonly Dictionary<string, string> _returnKeyByScreen = new();

    GameObject _lastPolledSelection; // to avoid extra writes
// ScreenController.cs  (add near the top of the class)
    private static ScreenController s_active;
// Add this method anywhere in the class
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void Log(string msg) => Debug.Log($"[ScreenController@{name}] {msg}");
    static bool IsUnder(GameObject root, GameObject child)
        => root && child && child.transform.IsChildOf(root.transform);

    public IUIScreen CurrentScreen => stack != null && stack.Count > 0 ? stack.Peek() : null;
    public GameObject CurrentScreenRoot => CurrentScreen != null ? CurrentScreen.Root : null;

    private static bool IsValidSelectable(GameObject go)
    {
        if (go == null || !go.activeInHierarchy) return false;
        var sel = go.GetComponent<Selectable>();
        return sel != null && sel.IsActive() && sel.interactable;
    }

    // --- CanvasGroup helpers ---
    static CanvasGroup EnsureCanvasGroup(GameObject go)
    {
        if (!go) return null;
        var cg = go.GetComponent<CanvasGroup>();
        if (!cg) cg = go.AddComponent<CanvasGroup>();
        return cg;
    }
    static void SetCGInteractable(GameObject go, bool on)
    {
        var cg = EnsureCanvasGroup(go);
        if (!cg) return;
        cg.blocksRaycasts = on;
        cg.interactable   = on;
    }
    static void SetCGAlpha(GameObject go, float a)
    {
        var cg = EnsureCanvasGroup(go);
        if (!cg) return;
        cg.alpha = a;
    }

    void OnEnable()
    {
        Debug.Log($"[SC] ENABLE name={name} scene={gameObject.scene.name} active={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name} root={transform.root.name}");
        var active = SceneManager.GetActiveScene();

        if (s_active == null)
        {
            enabled = (gameObject.scene == active);
            if (enabled) s_active = this;
        }
        else if (s_active != this)
        {
            var weAreInActive = (gameObject.scene == active);
            var activeIsElse   = (s_active && s_active.gameObject.scene != active);

            if (weAreInActive && activeIsElse)
            {
                s_active.enabled = false;
                s_active = this;
                enabled  = true;
            }
            else
            {
                enabled = false;
            }
        }

        SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    void OnDisable()
    {
        Debug.Log($"[SC] DISABLE name={name} scene={gameObject.scene.name}");
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        if (s_active == this) s_active = null;
    }

    void OnActiveSceneChanged(Scene oldScene, Scene newScene)
    {
        if (gameObject.scene == newScene)
        {
            if (s_active && s_active != this) s_active.enabled = false;
            s_active = this;
            enabled  = true;
        }
        else
        {
            if (s_active == this) s_active = null;
            enabled = false;
        }
    }

    // select next frame with an optional preferred object
    IEnumerator SelectNextFrame(IUIScreen screen, GameObject prefer = null)
    {
        var es = EventSystem.current;
        for (int i = 0; i < 6; i++)
        {
            yield return null;
            if (!es) es = EventSystem.current;
            if (!es) continue;

            GameObject target = prefer;

            // prefer remembered
            if (!target && _lastSelectedByScreen.TryGetValue(screen.ScreenId, out var remembered))
            {
                if (remembered && remembered.activeInHierarchy)
                {
                    var sel = remembered.GetComponent<UnityEngine.UI.Selectable>();
                    if (sel && sel.IsInteractable()) target = remembered;
                }
            }

            // cross-scene by key
            if (!target && SelectionMemory.TryGet(screen.ScreenId, out var key))
            {
                var byKey = FindByKeyUnder(screen.Root, key);
                if (IsValidSelectable(byKey)) target = byKey;
            }

            // explicit FirstSelected
            if (!target) target = screen.FirstSelected;

            // then any interactable Selectable under this screen
            if (!target && screen.Root)
            {
                var sel = screen.Root.GetComponentInChildren<UnityEngine.UI.Selectable>(includeInactive:false);
                if (sel && sel.IsInteractable()) target = sel.gameObject;
            }

            if (!target) continue;

            es.SetSelectedGameObject(target);
            if (es.currentSelectedGameObject == target) yield break;
        }
    }

    void Awake()
    {
        map = new Dictionary<string, IUIScreen>(screenBehaviours.Count);
        foreach (var mb in screenBehaviours)
        {
            if (mb is not IUIScreen s || s.Root == null) continue;
            map[s.ScreenId] = s;

            var cg = s.Root.GetComponent<CanvasGroup>() ?? s.Root.AddComponent<CanvasGroup>();
            cg.alpha = 0f; cg.interactable = false; cg.blocksRaycasts = false;
            s.Root.SetActive(false);
        }
    }

    void Start()
    {
        if (!autoShowOnStart) return;
        if (!string.IsNullOrEmpty(SceneFlow.FrontEndStartupScreenId)) return;

        if (!string.IsNullOrEmpty(initialScreenId) && map.TryGetValue(initialScreenId, out _))
            Show(initialScreenId);
    }

    private void BeginTransition()  => _transitioning = true;
    private void EndTransition()    => _transitioning = false;

    void Update()
    {
        if (s_active != this) return;  // only the active controller drives selection
        if (EventSystem.current == null || stack.Count == 0) return;

        var top = stack.Peek();
        var cur = EventSystem.current.currentSelectedGameObject;

        // Keep modal focus if open
        var modal = ModalHub.I != null ? ModalHub.I.Confirm : null;
        if (modal != null && modal.IsOpen)
        {
            if (!IsDescendantOf(cur, modal.gameObject))
            {
                var toSelectModal = modal.FirstSelected != null ? modal.FirstSelected : modal.gameObject;
                SafeSelect(toSelectModal);
            }
            return;
        }

        // Don’t learn while transitioning
        if (_transitioning) return;

        // Learn ONLY keyed items (menu entries), not sliders/toggles/dropdowns
        if (IsDescendantOf(cur, top.Root) && IsValidSelectable(cur))
        {
            var keyNow = KeyOf(cur);
            if (!string.IsNullOrEmpty(keyNow))
            {
                _lastSelectedByScreen[top.ScreenId] = cur;
                SelectionMemory.Save(top.ScreenId, keyNow);
#if UNITY_EDITOR
                Debug.Log($"[ScreenController] Update: learned {top.ScreenId} = {cur?.name}, key={keyNow}");
#endif
            }
        }

        // If selection is lost or pointing elsewhere, restore
        bool lostOrElsewhere = cur == null || !cur.activeInHierarchy || !IsDescendantOf(cur, top.Root);
        if (lostOrElsewhere)
        {
            GameObject toSelect = null;

            // 1) Same-scene remembered GO
            if (_lastSelectedByScreen.TryGetValue(top.ScreenId, out var remembered) &&
                IsDescendantOf(remembered, top.Root) && IsValidSelectable(remembered))
            {
                toSelect = remembered;
            }
            else
            {
                // 2) Cross-scene restore by key
                if (SelectionMemory.TryGet(top.ScreenId, out var key))
                {
                    var byKey = FindByKeyUnder(top.Root, key);
                    if (IsValidSelectable(byKey))
                        toSelect = byKey;
                }

                // 3) FirstSelected as final fallback
                if (!toSelect)
                    toSelect = top.FirstSelected;
            }

            SafeSelect(toSelect);
#if UNITY_EDITOR
            Debug.Log($"[ScreenController] Update: restored selection for {top.ScreenId} -> {(toSelect ? toSelect.name : "NULL")}");
#endif
            StartCoroutine(SelectNextFrame(top, toSelect));
        }
    }

    private static void SafeSelect(GameObject go)
    {
        if (go != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(go);
    }

    private static bool IsDescendantOf(GameObject child, GameObject root)
    {
        if (child == null || root == null) return false;
        var t = child.transform;
        while (t != null)
        {
            if (t.gameObject == root) return true;
            t = t.parent;
        }
        return false;
    }

    public void Show(string id)
    {
        StartCoroutine(CoShow(id, null));
    }

    public void Push(string id, object args = null)
    {
        StopAllCoroutines();
        StartCoroutine(CoPush(id, args));
    }

    public void Pop()
    {
#if UNITY_EDITOR
        Debug.Log($"[ScreenController@{name}] Pop requested, stackCount={stack?.Count ?? 0} scene={gameObject.scene.name}");
#endif
        if (stack.Count == 0) return;
        StopAllCoroutines();
        StartCoroutine(CoPop());
    }

    // --- Internals ---

    IEnumerator CoShow(string id, object args)
    {
        if (string.IsNullOrEmpty(id) || !map.TryGetValue(id, out var next))
            yield break;

        if (_transitioning) yield break;
        _transitioning = true;

        var prev = CurrentScreen;

        // capture currently selected on the PREVIOUS screen (before we touch CGs)
        var es = EventSystem.current;
        if (prev != null && es && IsUnder(prev.Root, es.currentSelectedGameObject))
        {
            _lastSelectedByScreen[prev.ScreenId] = es.currentSelectedGameObject;

            var keyPrev = KeyOf(es.currentSelectedGameObject);
            if (!string.IsNullOrEmpty(keyPrev))
            {
                SelectionMemory.Save(prev.ScreenId, keyPrev);
                _returnKeyByScreen[prev.ScreenId] = keyPrev; // also use as return-to hint
            }
        }

        // clear selection during transition
        if (es) es.SetSelectedGameObject(null);

        // If we're already on it, just ensure visible & interactive and re-select.
        if (prev == next)
        {
            if (next.Root) next.Root.SetActive(true);
            SetCGAlpha(next.Root, 1f);
            SetCGInteractable(next.Root, true);
            next.OnShow(args);
            StartCoroutine(SelectNextFrame(next));
            _transitioning = false;
            yield break;
        }

        // Prepare CGs
        var nextCG = EnsureCanvasGroup(next.Root);
        CanvasGroup prevCG = null;
        if (prev != null) prevCG = EnsureCanvasGroup(prev.Root);

        // Activate next
        if (next.Root && !next.Root.activeSelf) next.Root.SetActive(true);
        if (nextCG)
        {
            nextCG.DOKill();
            nextCG.alpha = fadeOnSwitch ? 0f : 1f;
            nextCG.blocksRaycasts = true;
            nextCG.interactable = !fadeOnSwitch;
        }
        next.OnShow(args);

        // Fades
        Tween prevTween = null;
        if (pushHidesPrevious && prev != null)
        {
            if (fadeOnSwitch && prevCG)
            {
                prevCG.DOKill();
                prevCG.blocksRaycasts = true;
                prevCG.interactable   = false;
                prevTween = prevCG.DOFade(0f, fadeDuration).SetEase(easeOut).SetUpdate(true);
            }
            else
            {
                SetCGAlpha(prev.Root, 0f);
                SetCGInteractable(prev.Root, false);
            }
        }

        Tween nextTween = null;
        if (fadeOnSwitch && nextCG)
        {
            nextTween = nextCG.DOFade(1f, fadeDuration).SetEase(easeIn).SetUpdate(true);
        }
        else
        {
            SetCGAlpha(next.Root, 1f);
            SetCGInteractable(next.Root, true);
        }

        // Wait
        if (prevTween != null) yield return prevTween.WaitForCompletion();
        if (nextTween != null) yield return nextTween.WaitForCompletion();

        // clear selection again after fades
        if (es) es.SetSelectedGameObject(null);

        // Finalize previous
        if (pushHidesPrevious && prev != null)
        {
            prev?.OnHide();
            if (prevCG) { prevCG.blocksRaycasts = false; prevCG.interactable = false; }
            if (prev.Root) prev.Root.SetActive(false);
        }

        // Finalize next
        if (nextCG) { nextCG.blocksRaycasts = true; nextCG.interactable = true; }

        // Preferred selection for 'next': remembered → key → FirstSelected
        GameObject preferred = null;

        if (_lastSelectedByScreen.TryGetValue(next.ScreenId, out var remembered) &&
            remembered && remembered.activeInHierarchy)
        {
            var sel = remembered.GetComponent<UnityEngine.UI.Selectable>();
            if (sel && sel.IsInteractable()) preferred = remembered;
        }

        if (!preferred && SelectionMemory.TryGet(next.ScreenId, out var keyNext))
        {
            var byKey = FindByKeyUnder(next.Root, keyNext);
            if (IsValidSelectable(byKey)) preferred = byKey;
        }

        // Update stack to only show 'next'
        if (stack == null) stack = new Stack<IUIScreen>();
        stack.Clear();
        stack.Push(next);

        // Select AFTER fade
        StartCoroutine(SelectNextFrame(next, preferred));
#if UNITY_EDITOR
        Debug.Log($"[ScreenController] CoShow: will select for {next.ScreenId} prefer={(preferred ? preferred.name : "NULL")}");
#endif

        _transitioning = false;
    }

    void LateUpdate()
    {
        
        var es = EventSystem.current;
        var scr = CurrentScreen;
        if (scr != null && scr.Root)
        {
            var cg = scr.Root.GetComponent<CanvasGroup>();
            if (cg && (!cg.interactable || !cg.blocksRaycasts || cg.alpha < 1f))
            {
                cg.alpha = 1f;
                cg.blocksRaycasts = true;
                cg.interactable   = true;
            }
        }
        
        if (!es || scr == null) return;

        var sel = es.currentSelectedGameObject;
        if (sel && sel != _lastPolledSelection && IsUnder(scr.Root, sel))
        {
            _lastPolledSelection = sel;
            // keep same-scene memory fresh (OK if this is unkeyed; cross-scene uses SelectionMemory/returnKey)
            _lastSelectedByScreen[scr.ScreenId] = sel;
        }
    }

    private static void SetSelected(GameObject go)
    {
        if (go == null || EventSystem.current == null) return;
        EventSystem.current.SetSelectedGameObject(go);
    }

    private IEnumerator CoPush(string id, object args)
    {
        BeginTransition();
        try
        {
            if (!map.TryGetValue(id, out var next)) yield break;

            // remember selection on current top (as return-to)
            if (stack.Count > 0 && EventSystem.current != null)
            {
                var topNow = stack.Peek();
                var cur = EventSystem.current.currentSelectedGameObject;
                if (IsDescendantOf(cur, topNow.Root) && IsValidSelectable(cur))
                    _lastSelectedByScreen[topNow.ScreenId] = cur;

                var key = KeyOf(cur);
                if (!string.IsNullOrEmpty(key))
                {
                    SelectionMemory.Save(topNow.ScreenId, key);
                    _returnKeyByScreen[topNow.ScreenId] = key; // <<< NEW
                }
            }

            if (pushHidesPrevious && stack.Count > 0)
            {
                var top = stack.Peek();
                yield return CoFadeOut(top);
                top.Root.SetActive(false);
            }

            next.Root.SetActive(true);
            next.OnShow(args);
            yield return CoFadeIn(next);

            if (!_lastSelectedByScreen.ContainsKey(next.ScreenId) && next.FirstSelected != null)
                _lastSelectedByScreen[next.ScreenId] = next.FirstSelected;

            if (EventSystem.current != null)
            {
                GameObject toSelect = null;

                if (_lastSelectedByScreen.TryGetValue(next.ScreenId, out var remembered) &&
                    IsDescendantOf(remembered, next.Root) && IsValidSelectable(remembered))
                {
                    toSelect = remembered;
                }
                else
                {
                    if (SelectionMemory.TryGet(next.ScreenId, out var key))
                    {
                        var byKey = FindByKeyUnder(next.Root, key);
                        if (IsValidSelectable(byKey))
                            toSelect = byKey;
                    }

                    if (!toSelect)
                        toSelect = next.FirstSelected;
                }

                SafeSelect(toSelect);
#if UNITY_EDITOR
                Debug.Log($"[ScreenController] CoPush: restored selection for {next.ScreenId} -> {(toSelect ? toSelect.name : "NULL")}");
#endif
            }

            stack.Push(next);
        }
        finally { EndTransition(); }
    }
    #if UNITY_EDITOR
    private static void CGLog(string where, GameObject root, string why)
    {
        var cg = root ? root.GetComponent<CanvasGroup>() : null;
        if (cg) Debug.Log($"[{where}] {root.name} CG: a={cg.alpha} i={cg.interactable} br={cg.blocksRaycasts}  ({why})");
    }
    #endif
    
    // ScreenController.cs
   private IEnumerator CoPop()
{
    // 1) Fade out + hide current top
    var top = stack.Pop();
    yield return CoFadeOut(top);
    top.OnHide();
    top.Root.SetActive(false);

    // 2) If popping left the stack empty, re-seed the initial screen (Title)
    if (stack.Count == 0)
    {
        if (!string.IsNullOrEmpty(initialScreenId) && map.TryGetValue(initialScreenId, out var seed))
        {
            seed.Root.SetActive(true);
            seed.OnShow(null);
            yield return CoFadeIn(seed);
            stack.Push(seed);

            GameObject toSelect = null;
            if (_lastSelectedByScreen.TryGetValue(seed.ScreenId, out var remembered) &&
                IsDescendantOf(remembered, seed.Root) && IsValidSelectable(remembered))
            {
                toSelect = remembered;
            }
            else if (SelectionMemory.TryGet(seed.ScreenId, out var key))
            {
                var byKey = FindByKeyUnder(seed.Root, key);
                if (IsValidSelectable(byKey)) toSelect = byKey;
            }
            if (!toSelect) toSelect = seed.FirstSelected;

            SafeSelect(toSelect);
            StartCoroutine(SelectNextFrame(seed, toSelect));
#if UNITY_EDITOR
            Debug.Log($"[ScreenController] CoPop: stack empty; re-seeded {seed.ScreenId} -> {(toSelect ? toSelect.name : "NULL")}");
#endif
            yield break;
        }

        // No initial screen configured: clear UI selection and stop
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        yield break;
    }

    // 3) Otherwise, reveal the screen underneath (normal path)
    var back = stack.Peek();

    back.Root.SetActive(true);
    back.OnShow(null);
    yield return CoFadeIn(back);

    // keep the revealed screen interactive
    var cg = back.Root.GetComponent<CanvasGroup>();
    if (cg) { cg.alpha = 1f; cg.blocksRaycasts = true; cg.interactable = true; }

    // choose something to select
    GameObject toSelect2 = null;
    if (_returnKeyByScreen.TryGetValue(back.ScreenId, out var returnKey))
    {
        var byKey = FindByKeyUnder(back.Root, returnKey);
        if (IsValidSelectable(byKey)) toSelect2 = byKey;
    }
    if (!toSelect2 && _lastSelectedByScreen.TryGetValue(back.ScreenId, out var remembered2) &&
        IsDescendantOf(remembered2, back.Root) && IsValidSelectable(remembered2))
    {
        toSelect2 = remembered2;
    }
    if (!toSelect2 && SelectionMemory.TryGet(back.ScreenId, out var key2))
    {
        var byKey2 = FindByKeyUnder(back.Root, key2);
        if (IsValidSelectable(byKey2)) toSelect2 = byKey2;
    }
    if (!toSelect2) toSelect2 = back.FirstSelected;

    SafeSelect(toSelect2);
    StartCoroutine(SelectNextFrame(back, toSelect2));
#if UNITY_EDITOR
    Debug.Log($"[ScreenController] CoPop: restored selection for {back.ScreenId} -> {(toSelect2 ? toSelect2.name : "NULL")}");
#endif
}


    private IEnumerator CoHideTop()
    {
        var top = stack.Pop();
        yield return CoFadeOut(top);
        top.OnHide();
        top.Root.SetActive(false);
    }

    private IEnumerator CoFadeIn(IUIScreen s)
    {
        var cg = s.Root.GetComponent<CanvasGroup>();
        if (!fadeOnSwitch || cg == null) { cg.alpha = 1; cg.interactable = true; cg.blocksRaycasts = true; yield break; }

        cg.DOKill();
        cg.alpha = 0f;
        cg.blocksRaycasts = true;
        Tween tw = cg.DOFade(1f, fadeDuration).SetEase(easeIn).SetUpdate(true);
        yield return tw.WaitForCompletion();
        cg.interactable = true;
    }

    private IEnumerator CoFadeOut(IUIScreen s)
    {
        var cg = s.Root.GetComponent<CanvasGroup>();
        if (!fadeOnSwitch || cg == null) { cg.alpha = 0; cg.interactable = false; cg.blocksRaycasts = false; yield break; }

        cg.DOKill();
        cg.interactable = false;
        Tween tw = cg.DOFade(0f, fadeDuration).SetEase(easeOut).SetUpdate(true);
        yield return tw.WaitForCompletion();
        cg.blocksRaycasts = false;
    }

    private static string KeyOf(GameObject go)
    {
        if (!go) return null;
        var k = go.GetComponent<SavedSelectableKey>();
        return k ? k.Key : null;
    }

    private static GameObject FindByKeyUnder(GameObject root, string key)
    {
        if (!root || string.IsNullOrEmpty(key)) return null;
        var all = root.GetComponentsInChildren<SavedSelectableKey>(true);
        foreach (var k in all)
            if (k.Key == key) return k.gameObject;
        return null;
    }
}
