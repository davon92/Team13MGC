using UnityEngine;
using UnityEngine.InputSystem;
using Yarn.Unity;

public class VNPauseInput : MonoBehaviour
{
    [SerializeField] ScreenController screens;   // assign in inspector
    [SerializeField] DialogueRunner runner;      // optional, not needed for advance now

    void Awake()
    {
        if (!screens) screens = FindObjectOfType<ScreenController>(true);
        if (!runner)  runner  = FindObjectOfType<DialogueRunner>(true);
    }

    // Start button -> toggle VN Pause (unless Save/Load is up)
    public void OnStart(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;
        if (screens == null) return;

        if (screens.IsOnTop(MenuIds.SaveLoad)) return; // ignore while in save/load

        if (screens.IsOnTop(MenuIds.VnPause)) screens.Pop();
        else screens.Push(MenuIds.VnPause);
    }

    // Cancel button -> back out of Save/Load, or close Pause
    public void OnCancel(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;
        if (screens == null) return;

        if (screens.IsOnTop(MenuIds.SaveLoad)) { screens.Pop(); return; }
        if (screens.IsOnTop(MenuIds.VnPause))  { screens.Pop(); return; }
    }

    // Advance is handled by DialogueAdvanceInput in Yarn 3, so do nothing here.
    public void OnAdvance(InputAction.CallbackContext _) { /* intentionally empty */ }
}