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

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (!runner) runner = FindFirstObjectByType<DialogueRunner>();
    }

    void Start()
    {
        if (!startAutomatically || runner == null) return;
        StartConversation(ResolveStartNode());
    }

    /// <summary>Starts a Yarn conversation at the given node (or the runner's Start Node if null/empty).</summary>
    public void StartConversation(string nodeName = null)
    {
        if (runner == null) { Debug.LogError("[VNBootstrap] DialogueRunner missing."); return; }
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
