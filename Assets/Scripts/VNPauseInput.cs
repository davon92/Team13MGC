using UnityEngine;
using UnityEngine.InputSystem;
using Yarn.Unity;

public class VNPauseInput : MonoBehaviour
{
    [SerializeField] ScreenController screens;
    [SerializeField] DialogueRunner runner;

    void Awake()
    {
        if (!screens) screens = FindObjectOfType<ScreenController>(true);
        if (!runner)  runner  = FindObjectOfType<DialogueRunner>(true);
    }

    // === Broadcast Messages path ===
    public void OnStart(InputValue v)
    {
        if (!v.isPressed) return;
        TogglePause();
    }

    public void OnCancel(InputValue v)
    {
        if (!v.isPressed) return;
        HandleCancel();
    }

    // === shared logic ===
    void TogglePause()
    {
        if (screens == null) return;
        if (screens.IsOnTop(MenuIds.SaveLoad)) return; // ignore while save/load is open

        if (screens.IsOnTop(MenuIds.VnPause)) screens.Pop();
        else screens.Push(MenuIds.VnPause);
    }

    void HandleCancel()
    {
        if (screens == null) return;

        if (screens.IsOnTop(MenuIds.SaveLoad)) { screens.Pop(); return; }
        if (screens.IsOnTop(MenuIds.VnPause))  { screens.Pop(); return; }
    }

    public void OnAdvance(InputAction.CallbackContext _) { /* Yarn handles advance */ }
}