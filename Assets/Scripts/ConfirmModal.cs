using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

[RequireComponent(typeof(CanvasGroup))]
public class ConfirmModal : MonoBehaviour, ICancelHandler
{
    [Header("Wiring")]
    [SerializeField] private CanvasGroup cg;                // on same GO
    [SerializeField] private TMP_Text messageLabel;         // optional
    [SerializeField] private TMP_Text countdownLabel;       // optional
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;
    [SerializeField] private GameObject firstSelected;      // default focus inside modal
    public bool IsOpen => isOpen;
    public GameObject FirstSelected => firstSelected;
    [Header("Animation")]
    [SerializeField, Range(0.05f, 0.5f)] private float fadeDuration = 0.15f;

    // state
    private TaskCompletionSource<bool> tcs;
    private bool isOpen;
    private bool timeoutDefault;
    private float countdownRemaining;
    private GameObject prevSelected;

    void Reset()
    {
        cg = GetComponent<CanvasGroup>();
    }

    void Awake()
    {
        if (cg == null) cg = GetComponent<CanvasGroup>();
        if (yesButton) yesButton.onClick.AddListener(() => Close(true));
        if (noButton)  noButton.onClick.AddListener(()  => Close(false));
        CloseImmediate(); // start hidden
    }

    void Update()
    {
        if (!isOpen || countdownRemaining <= 0f) return;
        countdownRemaining -= Time.unscaledDeltaTime;
        if (countdownLabel)
        {
            var secs = Mathf.CeilToInt(Mathf.Max(0f, countdownRemaining));
            countdownLabel.text = $"({secs}s)";
        }
        if (countdownRemaining <= 0f)
        {
            Close(timeoutDefault);
        }
    }

    public void OnCancel(BaseEventData _) => Close(false);

    /// <summary>
    /// Show the modal and await a boolean result. Returns true for Yes, false for No/timeout.
    /// </summary>
    /// <param name="message">Optional message string.</param>
    /// <param name="seconds">0 = no countdown.</param>
    /// <param name="defaultOnTimeout">Result returned if timer expires.</param>
    /// <param name="focus">Optional object to receive focus (defaults to firstSelected).</param>
    public Task<bool> ShowAsync(string message = null, float seconds = 0f, bool defaultOnTimeout = false, GameObject focus = null)
    {
        // Finish any previous prompt
        if (isOpen) Close(false);

        tcs = new TaskCompletionSource<bool>();
        isOpen = true;
        timeoutDefault = defaultOnTimeout;
        countdownRemaining = Mathf.Max(0f, seconds);

        if (messageLabel) messageLabel.text = string.IsNullOrEmpty(message) ? "" : message;
        if (countdownLabel) countdownLabel.text = seconds > 0 ? $"({Mathf.CeilToInt(seconds)}s)" : "";

        // remember what was selected before opening
        prevSelected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;

        // show + fade in
        gameObject.SetActive(true);
        cg.DOKill();
        cg.alpha = 0f;
        cg.blocksRaycasts = true;
        cg.interactable = false;
        cg.DOFade(1f, fadeDuration).SetUpdate(true).OnComplete(() => {
            cg.interactable = true;
        });

        // focus inside the modal
        var target = focus ? focus : firstSelected;
        if (target && EventSystem.current) EventSystem.current.SetSelectedGameObject(target);

        return tcs.Task;
    }

    public void Close(bool result)
    {
        if (!isOpen) return;
        isOpen = false;

        cg.DOKill();
        cg.interactable = false;
        // fade out, then actually hide and restore focus
        cg.DOFade(0f, fadeDuration).SetUpdate(true).OnComplete(() => {
            CloseImmediate();
            // restore previous selection if possible
            if (prevSelected && EventSystem.current)
                EventSystem.current.SetSelectedGameObject(prevSelected);
            prevSelected = null;
            tcs?.TrySetResult(result);
        });
    }

    private void CloseImmediate()
    {
        if (cg == null) cg = GetComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.blocksRaycasts = false;
        cg.interactable = false;
        gameObject.SetActive(false);
    }
}
