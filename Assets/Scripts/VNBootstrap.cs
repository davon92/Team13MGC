using System.Collections;
using UnityEngine;
using Yarn.Unity;

public class VNBootstrap : MonoBehaviour
{
    [Header("Yarn")]
    [SerializeField] private DialogueRunner runner;

    [Header("Start Settings")]
    [Tooltip("If true, start the VN automatically when this scene loads.")]
    [SerializeField] private bool startAutomatically = true;

    [Tooltip("One-shot explicit node to start at; takes priority over everything else.")]
    [SerializeField] private string startNodeOverride = "";

    [Tooltip("Fallback chapter/node if nothing else is pending.")]
    [SerializeField] private string firstChapterId = "Prologue";

    public static VNBootstrap Instance { get; private set; }
    public DialogueRunner Runner => runner;

    [Header("Initial Visual State (optional)")]
    [SerializeField] CanvasGroup linePresenterGroup;    // Line Presenter canvas group
    [SerializeField] CanvasGroup optionsPresenterGroup; // Options Presenter canvas group

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (!runner) runner = FindFirstObjectByType<DialogueRunner>(FindObjectsInactive.Include);

        // Track current node so saves know where to resume
        if (runner != null)
            runner.onNodeStart.AddListener(n => VNSaveManager.LastNode = n);
    }

    IEnumerator Start()
    {
        if (!startAutomatically || runner == null) yield break;

        // wait a frame so presenters are active and won’t flicker
        yield return null;

        // Ensure a clean starting visibility (prevents the “options on top” flash)
        if (linePresenterGroup)    linePresenterGroup.alpha    = 0f; // LinePresenter will fade itself in
        if (optionsPresenterGroup) optionsPresenterGroup.alpha = 0f; // keep options hidden until Yarn shows options

        // Resolve start node, then apply any pending save-slot snapshot (variables + node)
        var node = ResolveStartNode();
        ApplyPendingSnapshot(ref node);

        StartConversation(node);
    }

    public void StartConversation(string nodeName = null)
    {
        if (runner == null) return;
        if (runner.IsDialogueRunning) runner.Stop();

        var start = string.IsNullOrWhiteSpace(nodeName)
            ? (string.IsNullOrEmpty(runner.startNode) ? firstChapterId : runner.startNode)
            : nodeName;

        runner.StartDialogue(start);
#if UNITY_EDITOR
        Debug.Log($"[VNBootstrap] StartDialogue('{start}')");
#endif
    }

    /// <summary>Stops the current Yarn conversation, if any.</summary>
    public void StopConversation()
    {
        if (runner && runner.IsDialogueRunning) runner.Stop();
    }

    /// <summary>Computes the node to begin at, in priority order (override → return-from-rhythm → queued-load chapter → fallback).</summary>
    private string ResolveStartNode()
    {
        // 1) explicit one-shot override
        if (!string.IsNullOrWhiteSpace(startNodeOverride))
        {
            var n = startNodeOverride;
            startNodeOverride = ""; // consume once
            return n;
        }

        // 2) coming back from rhythm?
        if (!string.IsNullOrEmpty(SceneFlow.PendingVNStartNode))
        {
            var n = SceneFlow.PendingVNStartNode;
            SceneFlow.PendingVNStartNode = null; // consume once
            return n;
        }

        // 3) queued load? (chapter fallback only; variables applied in ApplyPendingSnapshot)
        if (SaveSystem.PendingLoadSlot.HasValue)
        {
            var slot    = SaveSystem.PendingLoadSlot.Value;
            var profile = SaveSystem.LoadFromSlot(slot);
            // (do not ClearPending here; ApplyPendingSnapshot will do it)
            if (!string.IsNullOrEmpty(profile?.chapterId))
                return profile.chapterId;
        }

        // 4) fallback
        return string.IsNullOrEmpty(firstChapterId) ? runner.startNode : firstChapterId;
    }

    /// <summary>
    /// If a save-slot load was queued, apply Yarn variables and refine the resume node.
    /// </summary>
    private void ApplyPendingSnapshot(ref string node)
    {
        if (!SaveSystem.PendingLoadSlot.HasValue) return;

        var slot    = SaveSystem.PendingLoadSlot.Value;
        var profile = SaveSystem.LoadFromSlot(slot);
        SaveSystem.ClearPending();

        if (profile == null) return;

        // NOTE: ensure SaveSystem.ProfileData has `public string yarnVariablesJson;`
        var json = profile.yarnVariablesJson;

        // If we have a variables blob, apply it and prefer the resume node it contains
        if (!string.IsNullOrEmpty(json) &&
            VNSaveManager.TryApplyJson(runner, json, out var resumeFromVars))
        {
            if (!string.IsNullOrEmpty(resumeFromVars))
                node = resumeFromVars;
            else if (!string.IsNullOrEmpty(profile.chapterId))
                node = profile.chapterId;
        }
        else if (!string.IsNullOrEmpty(profile.chapterId))
        {
            // No variables blob; at least jump to the saved chapter
            node = profile.chapterId;
        }
    }
}
