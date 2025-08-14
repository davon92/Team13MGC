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
    [SerializeField] private CanvasGroup cg;           // on the same GO
    [SerializeField] private TMP_Text    messageLabel; // optional
    [SerializeField] private TMP_Text    countdownLabel; // optional
    [SerializeField] private Button      yesButton;
    [SerializeField] private Button      noButton;
    [SerializeField] private GameObject  firstSelected; // defaults to yesButton

    [Header("Animation")]
    [SerializeField] private float fadeDuration = 0.15f;

    bool isOpen;
    float countdownRemaining;
    bool timeoutDefault = false; // false = No by default
    TaskCompletionSource<bool> tcs;

    void Reset()
    {
        cg = GetComponent<CanvasGroup>();
    }

    void Awake()
    {
        if (!cg) cg = GetComponent<CanvasGroup>();
        CloseImmediate();

        if (yesButton) yesButton.onClick.AddListener(() => Close(true));
        if (noButton)  noButton.onClick.AddListener(() => Close(false));
        if (!firstSelected && yesButton) firstSelected = yesButton.gameObject;
    }

    void Update()
    {
        if (!isOpen || countdownRemaining <= 0f) return;

        countdownRemaining -= Time.unscaledDeltaTime;
        if (countdownLabel)
            countdownLabel.text = $"({Mathf.CeilToInt(Mathf.Max(0f, countdownRemaining))}s)";

        if (countdownRemaining <= 0f)
            Close(timeoutDefault);
    }

    public void OnCancel(BaseEventData eventData) => Close(false);

    /// <summary>
    /// Show the modal and await a boolean result. Returns true for Yes, false for No/timeout.
    /// </summary>
    /// <param name="message">Optional message string.</param>
    /// <param name="seconds">0 = no countdown.</param>
    /// <param name="defaultOnTimeout">Result returned if timer expires.</param>
    /// <param name="focus">Optional object to receive focus (defaults to Yes).</param>
    public Task<bool> ShowAsync(string message = null, float seconds = 0f, bool defaultOnTimeout = false, GameObject focus = null)
    {
        if (isOpen) Close(false); // finish any previous prompt

        tcs = new TaskCompletionSource<bool>();
        isOpen = true;
        timeoutDefault = defaultOnTimeout;
        countdownRemaining = Mathf.Max(0f, seconds);

        if (messageLabel && !string.IsNullOrEmpty(message))
            messageLabel.text = message;
        if (countdownLabel)
            countdownLabel.text = seconds > 0 ? $"({Mathf.CeilToInt(seconds)}s)" : "";

        gameObject.SetActive(true);
        cg.interactable = true; cg.blocksRaycasts = true;
        
        cg.alpha = 0f;
        cg.DOKill();
        cg.DOFade(1f, fadeDuration).SetUpdate(true);
        cg.alpha = 1f;


        EventSystem.current.SetSelectedGameObject(focus ? focus : firstSelected);
        return tcs.Task;
    }

    /// <summary>Show using a callback instead of async/await.</summary>
    public async void Show(string message, float seconds, bool defaultOnTimeout, GameObject focus, Action<bool> onDone)
    {
        bool result = await ShowAsync(message, seconds, defaultOnTimeout, focus);
        onDone?.Invoke(result);
    }

    public void Close(bool result)
    {
        if (!isOpen) return;
        isOpen = false;
        
        cg.DOKill();
        cg.DOFade(0f, fadeDuration).SetUpdate(true).OnComplete(CloseImmediate);
        CloseImmediate();
        tcs?.TrySetResult(result);
    }

    void CloseImmediate()
    {
        cg.alpha = 0f;
        cg.interactable = false; cg.blocksRaycasts = false;
        gameObject.SetActive(false);
    }
}
