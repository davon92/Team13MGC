using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class CreditsScreen : MonoBehaviour, IUIScreen
{
    [Header("Wiring")]
    [SerializeField] private GameObject root;            // Screen_Credits panel
    [SerializeField] private GameObject firstSelected;   // Back button
    [SerializeField] private ScreenController screens;
    [SerializeField] private Button backButton;   // ← add this

    [Header("Static / Dynamic content (optional)")]
    [SerializeField] private CreditsData data;                 // Optional SO
    [SerializeField] private Transform namesParent;            // Optional: container to spawn rows under
    [SerializeField] private TextMeshProUGUI nameRowPrefab;    // Optional: prefab for a single name row
    [SerializeField] private TextMeshProUGUI titleLabel;       // Optional title TMP

    [Header("Auto-roll (optional)")]
    [SerializeField] private ScrollRect scrollRect;      // Assign if you want rolling credits
    [SerializeField] private float startDelay = 0.5f;
    [SerializeField] private float pixelsPerSecond = 80f;
    [SerializeField] private bool loop = false;

    private Tween rollTween;
    private readonly List<TextMeshProUGUI> spawnedRows = new();

    public string ScreenId => MenuIds.Credits;
    public GameObject Root => root != null ? root : gameObject;
    public GameObject FirstSelected => firstSelected;

    void Awake()
    {
        if (root == null) root = gameObject;
        // Auto-wire the button so you don’t have to in the Inspector
        if (backButton != null)
            backButton.onClick.AddListener(OnBack);
    }

    public void OnShow(object args)
    {
        BuildDynamicListIfNeeded();
        StartRollIfConfigured();

        if (FirstSelected)
            EventSystem.current.SetSelectedGameObject(FirstSelected);
    }

    public void OnHide() => KillRoll();

    // --- UI hooks ---
    public void OnBack()
    {
        KillRoll();
        screens?.Pop();
    }
    
    public void OnCancel(BaseEventData eventData) => OnBack();

    // --- Dynamic list (optional) ---
    private void BuildDynamicListIfNeeded()
    {
        if (namesParent == null || nameRowPrefab == null) return;   // using your static labels—nothing to do

        if (spawnedRows.Count == 0) // build once
        {
            var list = data != null && data.names != null ? data.names : new List<string>();
            foreach (var n in list)
            {
                var row = Instantiate(nameRowPrefab, namesParent);
                row.text = n;
                spawnedRows.Add(row);
            }
        }

        if (titleLabel && data) titleLabel.text = string.IsNullOrEmpty(data.title) ? "Credits" : data.title;

        // Ensure layout is up to date before we compute scroll distances
        if (namesParent is RectTransform rt)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
    }

    // --- Auto-scroll (optional) ---
    private void StartRollIfConfigured()
    {
        if (scrollRect == null || scrollRect.content == null) return;

        KillRoll();

        // Start at top
        scrollRect.verticalNormalizedPosition = 1f;

        float contentHeight = scrollRect.content.rect.height;
        float viewHeight    = scrollRect.viewport != null ? scrollRect.viewport.rect.height : (scrollRect.transform as RectTransform).rect.height;
        float distance = Mathf.Max(0f, contentHeight - viewHeight);
        if (distance < 1f || pixelsPerSecond <= 0f) return;

        float duration = distance / pixelsPerSecond;

        rollTween = DOTween.To(
                        () => scrollRect.verticalNormalizedPosition,
                        v  => scrollRect.verticalNormalizedPosition = v,
                        0f,                       // end at bottom
                        duration)
                    .SetDelay(startDelay)
                    .SetEase(Ease.Linear)
                    .SetUpdate(true)            // unscaled time
                    .OnComplete(() =>
                    {
                        if (loop)
                        {
                            scrollRect.verticalNormalizedPosition = 1f;
                            StartRollIfConfigured();
                        }
                    });
    }

    private void KillRoll()
    {
        if (rollTween != null && rollTween.IsActive())
            rollTween.Kill();
    }
}
