using UnityEngine;
using Yarn.Unity;

/// <summary>
/// Thin wrapper around Yarn Spinner 3.x's DialogueRunner.
/// Keeps all logic free of legacy events/fields so it compiles cleanly.
/// Rely on Yarn's built-in presenters (e.g., LineView, OptionsListView)
/// that you wire in the DialogueRunner's "Dialogue Presenters" list in the Inspector.
/// </summary>
public class VNController : MonoBehaviour
{
    [Header("Yarn")]
    [SerializeField] private DialogueRunner runner;

    [Tooltip("If true, call StartDialogue() on Start using the runner's Start Node.")]
    [SerializeField] private bool startAutomatically = false;

    [Tooltip("Optional explicit start node name. If empty, runner's Start Node is used.")]
    [SerializeField] private string startNodeOverride = "";

    public static VNController Instance { get; private set; }

    public bool IsRunning => runner != null && runner.IsDialogueRunning;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (runner == null)
            runner = FindObjectOfType<DialogueRunner>(includeInactive: true);
    }

    private void Start()
    {
        if (startAutomatically && runner != null)
        {
            StartConversation(string.IsNullOrWhiteSpace(startNodeOverride) ? null : startNodeOverride);
        }
    }

    /// <summary>
    /// Starts a Yarn conversation at the given node (or the runner's Start Node if null/empty).
    /// </summary>
    public void StartConversation(string nodeName = null)
    {
        if (runner == null) {
            Debug.LogError("[VNController] DialogueRunner reference is missing.");
            return;
        }

        if (runner.IsDialogueRunning) {
            runner.Stop();
        }

        if (string.IsNullOrWhiteSpace(nodeName))
        {
            // Use the DialogueRunner's configured Start Node
            runner.StartDialogue(runner.startNode);
        }
        else
        {
            runner.StartDialogue(nodeName);
        }
    }

    /// <summary>
    /// Stops the current conversation (if any).
    /// </summary>
    public void StopConversation()
    {
        if (runner == null)
            return;

        if (runner.IsDialogueRunning)
        {
            runner.Stop();
        }
    }

    /// <summary>
    /// Convenience helper; returns the attached DialogueRunner.
    /// </summary>
    public DialogueRunner GetRunner() => runner;
}
