using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;

public interface IUIScreen
{
    string ScreenId { get; }
    GameObject Root { get; }
    GameObject FirstSelected { get; }
    void OnShow(object args);
    void OnHide();
}

public class ScreenController : MonoBehaviour
{
    [Header("Screens (drag each screen script here)")]
    [SerializeField] private List<MonoBehaviour> screenBehaviours = new(); // each implements IUIScreen
    [SerializeField] private string initialScreenId = MenuIds.Title;

    [Header("Transition")]
    [SerializeField] private bool fadeOnSwitch = true;
    [SerializeField, Range(0f, 1f)] private float fadeDuration = 0.20f;
    [SerializeField] private Ease easeIn  = Ease.OutQuad; // when showing a screen
    [SerializeField] private Ease easeOut = Ease.InQuad;  // when hiding a screen

    private readonly Stack<IUIScreen> stack = new();
    private Dictionary<string, IUIScreen> map;

    void Awake()
    {
        map = new Dictionary<string, IUIScreen>(screenBehaviours.Count);
        foreach (var mb in screenBehaviours)
        {
            if (mb is not IUIScreen s || s.Root == null) continue;
            map[s.ScreenId] = s;

            // Ensure each screen has a CanvasGroup
            var cg = s.Root.GetComponent<CanvasGroup>() ?? s.Root.AddComponent<CanvasGroup>();
            cg.alpha = 0f; cg.interactable = false; cg.blocksRaycasts = false;
            s.Root.SetActive(false);
        }
    }

    void Start()
    {
        if (!string.IsNullOrEmpty(initialScreenId) && map.TryGetValue(initialScreenId, out _))
            Show(initialScreenId);
        else if (map.Count > 0)
            Show(map.Keys.First());
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

    // --- Internals (still coroutine-based to preserve stack flow) ---
    private IEnumerator CoShow(string id, object args)
    {
        while (stack.Count > 0)
            yield return CoHideTop();

        yield return CoPush(id, args);
    }

    private IEnumerator CoPush(string id, object args)
    {
        if (!map.TryGetValue(id, out var next)) yield break;

        if (stack.Count > 0)
        {
            var current = stack.Peek();
            yield return CoFadeOut(current);
        }

        stack.Push(next);
        next.Root.SetActive(true);
        next.OnShow(args);
        yield return CoFadeIn(next);

        if (next.FirstSelected)
            EventSystem.current.SetSelectedGameObject(next.FirstSelected);
    }

    private IEnumerator CoPop()
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

        if (back.FirstSelected)
            EventSystem.current.SetSelectedGameObject(back.FirstSelected);
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

        // kill any running tween on this target, then tween in
        cg.DOKill();
        cg.alpha = Mathf.Clamp01(cg.alpha);
        cg.interactable = false; cg.blocksRaycasts = false;

        Tween tw = cg.DOFade(1f, fadeDuration)
                   .SetEase(easeIn)
                   .SetUpdate(true); // unscaled time (menus)

        yield return tw.WaitForCompletion();

        cg.interactable = true; cg.blocksRaycasts = true;
    }

    private IEnumerator CoFadeOut(IUIScreen s)
    {
        var cg = s.Root.GetComponent<CanvasGroup>();
        if (!fadeOnSwitch || cg == null) { cg.alpha = 0; cg.interactable = false; cg.blocksRaycasts = false; yield break; }

        cg.DOKill();
        cg.interactable = false; cg.blocksRaycasts = false;

        Tween tw = cg.DOFade(0f, fadeDuration)
                   .SetEase(easeOut)
                   .SetUpdate(true);

        yield return tw.WaitForCompletion();
    }
}
