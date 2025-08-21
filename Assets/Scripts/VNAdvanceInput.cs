using UnityEngine;
using UnityEngine.InputSystem;
using Yarn.Unity;
using Yarn.Unity.Legacy;

public class VNAdvanceInput : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private DialogueRunner runner;           // your DialogueRunner in scene
    [SerializeField] private DialogueViewBase dialogueView;   // your VNDialogueView (it derives from DialogueViewBase)

    [Header("Input System")]
    [SerializeField] private InputActionReference advanceAction; // e.g. UI/Submit, or a custom "Advance" action

    private InputAction _action;

    private void OnEnable()
    {
        if (advanceAction != null)
        {
            _action = advanceAction.action;
            _action.performed += OnAdvancePerformed;
            _action.Enable();
        }
    }

    private void OnDisable()
    {
        if (_action != null)
        {
            _action.performed -= OnAdvancePerformed;
            _action.Disable();
        }
    }

    private void OnAdvancePerformed(InputAction.CallbackContext _)
    {
        // Only advance if dialogue is currently running and no other menu is on top.
        if (runner != null && runner.IsDialogueRunning && dialogueView != null)
        {
            dialogueView.UserRequestedViewAdvancement();
        }
    }

    // Optional: public method for hooking a UI Button's OnClick
    public void AdvanceViaUIButton()
    {
        OnAdvancePerformed(default);
    }
}