using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
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
    
    System.Collections.IEnumerator CoResumeFromPause()
    {
        VNInputBlocker.BlockAdvance(0.12f);
        screens.Pop();

        yield return null;
        yield return null;
        yield return VNOptionsFocusKeeper.RestoreAfterResume(1.0f);
    }

    void TogglePause()
    {
        if (screens == null) return;
        if (screens.IsOnTop(MenuIds.SaveLoad)) return;

        if (screens.IsOnTop(MenuIds.VnPause))
        {
            VNCo.Start(CoResumeFromPause());
        }
        else
        {
            // Remember which option was selected before Pause takes focus
            VNOptionsFocusKeeper.CaptureNow();
            screens.Push(MenuIds.VnPause);
        }
    }

    void HandleCancel()
    {
        if (screens == null) return;

        if (screens.IsOnTop(MenuIds.SaveLoad)) { screens.Pop(); return; }
        if (screens.IsOnTop(MenuIds.VnPause))  { screens.Pop(); return; }
    }

    public void OnAdvance(InputAction.CallbackContext _) { /* Yarn handles advance */ }
}