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
    [SerializeField] CanvasGroup linePresenterGroup;    // the CanvasGroup on your Line Presenter
    [SerializeField] CanvasGroup optionsPresenterGroup; // the CanvasGroup on your Options Presenter

    void Awake() {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (!runner) runner = FindFirstObjectByType<DialogueRunner>();
    }

    IEnumerator Start() {
        if (!startAutomatically || runner == null) yield break;
        yield return null; // wait a frame so presenters are active
        // 2) Ensure a clean starting visibility (prevents the “options on top” flash)
        if (linePresenterGroup)    linePresenterGroup.alpha    = 0f; // LinePresenter will fade itself in
        if (optionsPresenterGroup) optionsPresenterGroup.alpha = 0f; // keep options hidden until Yarn shows options
        StartConversation(ResolveStartNode());
    }

    public void StartConversation(string nodeName = null) {
        if (runner == null) return;
        if (runner.IsDialogueRunning) runner.Stop();
        var start = string.IsNullOrWhiteSpace(nodeName) ? runner.startNode : nodeName;
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

    /// <summary>Computes the node to begin at, in priority order.</summary>
    private string ResolveStartNode()
    {
        // 1) explicit one-shot override
        if (!string.IsNullOrWhiteSpace(startNodeOverride))
        {
            var n = startNodeOverride;
            startNodeOverride = "";     // consume once
            return n;
        }

        // 2) coming back from rhythm?
        if (!string.IsNullOrEmpty(SceneFlow.PendingVNStartNode))
        {
            var n = SceneFlow.PendingVNStartNode;
            SceneFlow.PendingVNStartNode = null;             // consume once
            return n;                                        // :contentReference[oaicite:5]{index=5}
        }

        // 3) queued load?
        if (SaveSystem.PendingLoadSlot.HasValue)
        {
            var slot = SaveSystem.PendingLoadSlot.Value;
            var profile = SaveSystem.LoadFromSlot(slot);     // :contentReference[oaicite:6]{index=6}
            SaveSystem.ClearPending();                       // :contentReference[oaicite:7]{index=7}
            if (!string.IsNullOrEmpty(profile?.chapterId))
                return profile.chapterId;                    // where to resume VN
        }

        // 4) fallback
        return string.IsNullOrEmpty(firstChapterId) ? runner.startNode : firstChapterId;
    }
}
