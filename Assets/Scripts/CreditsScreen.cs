using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using UnityEngine.InputSystem;

public class CreditsScreen : MonoBehaviour, IUIScreen
{
    [Header("Wiring")]
    [SerializeField] private GameObject root;                  // Screen_Credits panel
    [SerializeField] private GameObject firstSelected;         // Back button (or any button)
    [SerializeField] private ScreenController screens;
    [SerializeField] private Button backButton;

    [Header("Data")]
    [SerializeField] private CreditsData data;

    [Header("Content Parents")]
    [Tooltip("Container for generated rows (usually ScrollRect.content).")]
    [SerializeField] private RectTransform contentParent;

    [Header("Prefabs")]
    [SerializeField] private TextMeshProUGUI titlePrefab;      // Big title (optional)
    [SerializeField] private TextMeshProUGUI sectionHeaderPrefab;
    [SerializeField] private TextMeshProUGUI nameRowPrefab;    // For Kind.Name (legacy-friendly)
    [SerializeField] private TextMeshProUGUI nameRoleRowPrefab;// For Kind.NameWithRole
    [SerializeField] private TextMeshProUGUI paragraphPrefab;  // For Kind.Paragraph / license text
    [SerializeField] private Image           logoPrefab;       // For Kind.Logo
    [SerializeField] private GameObject      spacerPrefab;     // Empty GO with LayoutElement

    [Header("Auto-roll (optional)")]
    [SerializeField] private ScrollRect scrollRect; // assign your ScrollRect
    [SerializeField] private float startDelay = 0.5f;
    [SerializeField] private float pixelsPerSecond = 80f;
    [SerializeField] private bool loop = false;

    private Tween rollTween;
    private readonly List<Object> spawned = new(); // track things to clean if needed

    public string     ScreenId      => MenuIds.Credits;
    public GameObject Root          => root != null ? root : gameObject;
    public GameObject FirstSelected => firstSelected;

    void Awake()
    {
        if (root == null) root = gameObject;
        if (backButton != null) backButton.onClick.AddListener(OnBack);
    }

    public void OnShow(object args)
    {
        BuildCredits();
        StartRollIfConfigured();

        if (FirstSelected)
            EventSystem.current.SetSelectedGameObject(FirstSelected);
    }

    public void OnHide() => KillRoll();

    public void OnBack()
    {
        KillRoll();
        screens?.Pop();
    }

    public void OnUI_Cancel(InputValue _) => OnBack();

    // -------------------------
    // Content building
    // -------------------------
    private void BuildCredits()
    {
        if (!contentParent)
        {
            Debug.LogWarning("[Credits] No contentParent set.");
            return;
        }

        // Clear old (if you revisit this screen)
        for (int i = contentParent.childCount - 1; i >= 0; i--)
            Destroy(contentParent.GetChild(i).gameObject);
        spawned.Clear();

        // Title
        if (titlePrefab && data && !string.IsNullOrEmpty(data.title))
        {
            var t = Instantiate(titlePrefab, contentParent);
            t.text = data.title;
            spawned.Add(t);
        }

        // Rich sections path
        bool builtRich = data != null && data.sections != null && data.sections.Count > 0;
        if (builtRich)
        {
            foreach (var sec in data.sections)
                BuildSection(sec);
        }
        else
        {
            // Legacy names list path
            if (data != null && data.names != null && data.names.Count > 0 && nameRowPrefab)
            {
                foreach (var n in data.names)
                {
                    var row = Instantiate(nameRowPrefab, contentParent);
                    row.text = n;
                    spawned.Add(row);
                }
            }
        }

        // Append license files (if any)
        if (data != null && data.licenseFiles != null)
        {
            foreach (var ta in data.licenseFiles)
            {
                if (!ta) continue;
                // Section header "Licenses" (only once)
                // If you want a per-file header, move this inside the loop with ta.name.
                if (sectionHeaderPrefab)
                {
                    var hdr = Instantiate(sectionHeaderPrefab, contentParent);
                    hdr.text = "Software Licenses";
                    spawned.Add(hdr);
                    sectionHeaderPrefab = null; // ensure it's only added once; comment out to repeat per-block
                }

                if (paragraphPrefab)
                {
                    var para = Instantiate(paragraphPrefab, contentParent);
                    para.enableWordWrapping = true;
                    para.richText = false; // license text is usually plain
                    para.text = ta.text;
                    spawned.Add(para);
                }

                AddSpacer(24f);
            }
        }

        // Ensure layout is up to date before we compute scroll distances
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentParent);
    }

    private void BuildSection(CreditSection sec)
    {
        if (sec == null) return;

        if (!string.IsNullOrEmpty(sec.heading) && sectionHeaderPrefab)
        {
            var hdr = Instantiate(sectionHeaderPrefab, contentParent);
            hdr.text = sec.heading;
            spawned.Add(hdr);
        }

        if (sec.items == null) return;

        foreach (var it in sec.items)
        {
            if (it == null) continue;
            switch (it.kind)
            {
                case CreditItem.Kind.Name:
                {
                    if (!nameRowPrefab) break;
                    var row = Instantiate(nameRowPrefab, contentParent);
                    row.text = it.name;
                    spawned.Add(row);
                    break;
                }
                case CreditItem.Kind.NameWithRole:
                {
                    if (!nameRoleRowPrefab) break;
                    var row = Instantiate(nameRoleRowPrefab, contentParent);
                    // e.g. "Name — Role"
                    row.text = string.IsNullOrEmpty(it.role) ? it.name : $"{it.name} — {it.role}";
                    spawned.Add(row);
                    break;
                }
                case CreditItem.Kind.Paragraph:
                {
                    if (!paragraphPrefab) break;
                    var p = Instantiate(paragraphPrefab, contentParent);
                    p.enableWordWrapping = true;
                    p.text = it.paragraph;
                    spawned.Add(p);
                    break;
                }
                case CreditItem.Kind.Logo:
                {
                    if (!logoPrefab || !it.logo) break;
                    var img = Instantiate(logoPrefab, contentParent);
                    img.sprite = it.logo;
                    img.preserveAspect = true;
                    var le = img.GetComponent<LayoutElement>();
                    if (!le) le = img.gameObject.AddComponent<LayoutElement>();
                    le.preferredHeight = Mathf.Max(32f, it.logoHeight);
                    spawned.Add(img);
                    break;
                }
                case CreditItem.Kind.Spacer:
                {
                    AddSpacer(Mathf.Max(0f, it.spaceHeight));
                    break;
                }
            }
        }
    }

    private void AddSpacer(float height)
    {
        if (height <= 0f) return;
        if (spacerPrefab)
        {
            var go = Instantiate(spacerPrefab, contentParent);
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            le.minHeight = height;
            spawned.Add(go);
        }
        else
        {
            // Fallback: create an empty object with LayoutElement
            var go = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(contentParent, false);
            var le = go.GetComponent<LayoutElement>();
            le.minHeight = height;
            spawned.Add(go);
        }
    }

    // -------------------------
    // Auto-scroll
    // -------------------------
    private void StartRollIfConfigured()
    {
        if (scrollRect == null || scrollRect.content == null) return;

        KillRoll();

        // Start at top
        scrollRect.verticalNormalizedPosition = 1f;

        // Recalculate layout before measuring
        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);

        float contentHeight = scrollRect.content.rect.height;
        float viewHeight = scrollRect.viewport != null
            ? scrollRect.viewport.rect.height
            : (scrollRect.transform as RectTransform).rect.height;

        float distance = Mathf.Max(0f, contentHeight - viewHeight);
        if (distance < 1f || pixelsPerSecond <= 0f) return;

        float duration = distance / pixelsPerSecond;

        rollTween = DOTween.To(
                        () => scrollRect.verticalNormalizedPosition,
                        v  => scrollRect.verticalNormalizedPosition = v,
                        0f,
                        duration)
                    .SetDelay(startDelay)
                    .SetEase(Ease.Linear)
                    .SetUpdate(true)
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
