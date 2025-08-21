using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;
using UnityEngine.UI;

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

    private readonly Stack<IUIScreen> stack = new();
    private Dictionary<string, IUIScreen> map;
    private bool _transitioning;
    public bool   CanPop => stack != null && stack.Count > 0;
    public string TopId  => stack != null && stack.Count > 0 ? stack.Peek().ScreenId : null;
    public bool   IsOnTop(string screenId) => TopId == screenId;
    
    // Remember the last selected object per screen so we can restore it on return.
    private readonly Dictionary<string, GameObject> _lastSelectedByScreen = new();
    
    private static bool IsValidSelectable(GameObject go)
    {
        if (go == null || !go.activeInHierarchy) return false;
        var sel = go.GetComponent<Selectable>();
        return sel != null && sel.IsActive() && sel.interactable;
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
        if (!string.IsNullOrEmpty(initialScreenId) && map.TryGetValue(initialScreenId, out _))
            Show(initialScreenId);
    }
    
    private void BeginTransition()  => _transitioning = true;
    private void EndTransition()    => _transitioning = false;
    
    void Update()
    {
        if (EventSystem.current == null || stack.Count == 0) return;

        var top = stack.Peek();
        var cur = EventSystem.current.currentSelectedGameObject;

        // Honor an open ConfirmModal (so it keeps focus)
        var modal = ModalHub.I != null ? ModalHub.I.Confirm : null;
        if (modal != null && modal.IsOpen)
        {
            if (!IsDescendantOf(cur, modal.gameObject))
            {
                var toSelect = modal.FirstSelected != null ? modal.FirstSelected : modal.gameObject;
                SafeSelect(toSelect);
            }
            return;
        }

        // Do not learn/overwrite remembered selection during transitions
        if (_transitioning) return;

        if (IsDescendantOf(cur, top.Root) && IsValidSelectable(cur))
            _lastSelectedByScreen[top.ScreenId] = cur;

        bool lostOrElsewhere = cur == null || !cur.activeInHierarchy || !IsDescendantOf(cur, top.Root);
        if (lostOrElsewhere)
        {
            GameObject toSelect = null;
            if (_lastSelectedByScreen.TryGetValue(top.ScreenId, out var remembered)
                && IsDescendantOf(remembered, top.Root) && IsValidSelectable(remembered))
                toSelect = remembered;
            else
                toSelect = top.FirstSelected;

            SafeSelect(toSelect);
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


    public void Show(string id, object args = null)
    {
        StopAllCoroutines();
        StartCoroutine(CoShow(id, args));
    }

    public void Push(string id, object args = null)
    {
        StopAllCoroutines();
        StartCoroutine(CoPush(id, args));
    }

    public void Pop()
    {
        if (stack.Count <= 1) return;
        StopAllCoroutines();
        StartCoroutine(CoPop());
    }

    // --- Internals ---

    private IEnumerator CoShow(string id, object args)
    {
        while (stack.Count > 0)
            yield return CoHideTop();

        yield return CoPush(id, args);
    }


// Convenience to set selection safely
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

            // remember selection on current top
            if (stack.Count > 0 && EventSystem.current != null)
            {
                var topNow = stack.Peek();
                var cur = EventSystem.current.currentSelectedGameObject;
                if (IsDescendantOf(cur, topNow.Root) && IsValidSelectable(cur))
                    _lastSelectedByScreen[topNow.ScreenId] = cur;
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
                if (_lastSelectedByScreen.TryGetValue(next.ScreenId, out var remembered)
                    && IsDescendantOf(remembered, next.Root) && IsValidSelectable(remembered))
                    toSelect = remembered;
                else
                    toSelect = next.FirstSelected;

                SafeSelect(toSelect);
            }

            stack.Push(next);
        }
        finally { EndTransition(); }
    }
    
    private IEnumerator CoPop()
    {
        BeginTransition();
        try
        {
            if (stack.Count <= 1) yield break;

            var top = stack.Pop();
            yield return CoFadeOut(top);
            top.OnHide();
            top.Root.SetActive(false);

            var back = stack.Peek();
            back.Root.SetActive(true);
            back.OnShow(null);
            yield return CoFadeIn(back);

            if (EventSystem.current != null)
            {
                GameObject toSelect = null;
                if (_lastSelectedByScreen.TryGetValue(back.ScreenId, out var remembered)
                    && IsDescendantOf(remembered, back.Root) && IsValidSelectable(remembered))
                    toSelect = remembered;
                else
                    toSelect = back.FirstSelected;

                SafeSelect(toSelect);
            }
        }
        finally { EndTransition(); }
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
}
