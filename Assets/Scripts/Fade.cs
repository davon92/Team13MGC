using System.Threading.Tasks;
using UnityEngine;
using DG.Tweening;

[RequireComponent(typeof(CanvasGroup))]
public class Fade : MonoBehaviour
{
    public static Fade Instance { get; private set; }

    [SerializeField] private CanvasGroup cg;
    [SerializeField] private float defaultDuration = 0.25f;
    [SerializeField] private Ease ease = Ease.Linear;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (!cg) cg = GetComponent<CanvasGroup>();
        DontDestroyOnLoad(gameObject);

        cg.alpha = 0f;
        cg.blocksRaycasts = false;
        cg.interactable = false;
    }

    public Task In(float duration = -1f)  => FadeTo(0f, duration < 0 ? defaultDuration : duration);
    public Task Out(float duration = -1f) => FadeTo(1f, duration < 0 ? defaultDuration : duration);

    public Task FadeTo(float target, float duration)
    {
        var tcs = new TaskCompletionSource<bool>();
        cg.DOKill();

        // NEW: during the fade we deliberately block clicks
        cg.blocksRaycasts = true;
        cg.interactable   = true;   // NEW

        cg.DOFade(target, Mathf.Max(0.0001f, duration))
            .SetEase(ease)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                // NEW: after the fade finishes, only the “black” state should block
                bool isBlack = target > 0.99f;
                cg.blocksRaycasts = isBlack;
                cg.interactable   = isBlack;   // NEW
                tcs.TrySetResult(true);
            });

        return tcs.Task;
    }
}