using UnityEngine;
using UnityEngine.InputSystem;
using Yarn.Unity;

/// Attach this to the same GameObject that has your DialogueRunner
/// (or anywhere in the scene and assign the reference).
public class VNAdvanceInput : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] DialogueRunner runner;               // Dialogue System's DialogueRunner
    [SerializeField] ScreenController screens;   // NEW
    
    [Header("Input")]
    [SerializeField] InputActionReference advanceAction;  // e.g. Controls/Submit, Gamepad A, Mouse Left
    [SerializeField] bool enableOnStart = true;

    void Awake()
    {
        if (!runner) runner = FindFirstObjectByType<DialogueRunner>();
        if (!screens) screens = FindFirstObjectByType<ScreenController>(FindObjectsInactive.Include); // NEW
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
        // NEW: Do not advance while UI overlays are on top
        if (screens && (screens.IsOnTop(MenuIds.VnPause) || screens.IsOnTop(MenuIds.SaveLoad)))
            return;

        // Also drop one advance right after closing Pause
        if (VNInputBlocker.ShouldBlock) return;  // :contentReference[oaicite:1]{index=1}
        // In Yarn 3.x this requests the next piece of content.
        // If options are currently on screen, this is ignored (safe).
        runner.RequestNextLine();
    }
}