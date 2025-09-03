using UnityEngine;
using UnityEngine.InputSystem;
using Yarn.Unity;

/// Attach this to the same GameObject that has your DialogueRunner
/// (or anywhere in the scene and assign the reference).
public class VNAdvanceInput : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] DialogueRunner runner;               // Dialogue System's DialogueRunner

    [Header("Input")]
    [SerializeField] InputActionReference advanceAction;  // e.g. Controls/Submit, Gamepad A, Mouse Left
    [SerializeField] bool enableOnStart = true;

    void Awake()
    {
        if (!runner) runner = FindFirstObjectByType<DialogueRunner>();
        if (!runner) Debug.LogError("[VNAdvanceInput] No DialogueRunner found.");
        if (!advanceAction) Debug.LogWarning("[VNAdvanceInput] No InputActionReference assigned.");
    }

    void OnEnable()
    {
        if (enableOnStart) advanceAction?.action.Enable();
        if (advanceAction) advanceAction.action.performed += OnAdvance;
    }

    void OnDisable()
    {
        if (advanceAction) advanceAction.action.performed -= OnAdvance;
        if (enableOnStart) advanceAction?.action.Disable();
    }

    private void OnAdvance(InputAction.CallbackContext _)
    {
        // Only advance when a conversation is running
        if (runner == null || runner.IsDialogueRunning == false) return;

        // In Yarn 3.x this requests the next piece of content.
        // If options are currently on screen, this is ignored (safe).
        runner.RequestNextLine();
    }
}