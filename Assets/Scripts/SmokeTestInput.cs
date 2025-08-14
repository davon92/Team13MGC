using UnityEngine;
using UnityEngine.InputSystem;

public class SmokeTestInput : MonoBehaviour
{
    [SerializeField] PlayerInput playerInput;

    void Update()
    {
        // Device presence
        if (Gamepad.current == null && Keyboard.current == null)
            return;

        // Quick raw checks (bypass actions)
        if (Gamepad.current != null)
        {
            if (Gamepad.current.buttonSouth.wasPressedThisFrame) Debug.Log("Raw: South");
            if (Gamepad.current.buttonEast.wasPressedThisFrame)  Debug.Log("Raw: East");
            Vector2 ls = Gamepad.current.leftStick.ReadValue();
            //if (ls.sqrMagnitude > 0.001f) //Debug.Log($"Raw LS: {ls}");
        }

        if (Keyboard.current != null)
        {
            if (Keyboard.current.spaceKey.wasPressedThisFrame) Debug.Log("Raw: Space");
        }

        // Show current action map
        if (playerInput) Debug.Log($"Map: {playerInput.currentActionMap?.name}");
    }
}