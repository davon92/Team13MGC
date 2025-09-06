using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif

/// Put one of these in a bootstrap scene (or any scene that stays alive).
/// No inspector wiring needed.
public class InputModeWatcher : MonoBehaviour
{
#if ENABLE_INPUT_SYSTEM
    void Awake() { DontDestroyOnLoad(gameObject); }
    
    void OnEnable()  { InputSystem.onEvent += OnInputEvent; }
    void OnDisable() { InputSystem.onEvent -= OnInputEvent; }

    void OnInputEvent(InputEventPtr evt, InputDevice device)
    {
        if (!evt.IsA<StateEvent>() && !evt.IsA<DeltaStateEvent>()) return;
        if (device == null || !device.enabled) return;

        var detected = Classify(device);
        SettingsService.SetAutoDetectedGlyphStyle(detected);
    }

    static InputGlyphStyle Classify(InputDevice d)
    {
        if (d is Keyboard || d is Mouse) return InputGlyphStyle.KeyboardMouse;

        if (d is Gamepad)
        {
            // PlayStation families
            if (d is UnityEngine.InputSystem.DualShock.DualShockGamepad)
                return InputGlyphStyle.PlayStation;
            
            if (d is UnityEngine.InputSystem.Switch.SwitchProControllerHID)
                return InputGlyphStyle.Nintendo;

            // Default any other gamepads (XInput, generic) to Xbox glyphs
            return InputGlyphStyle.Xbox;
        }

        // Unknown -> don't change current
        return SettingsService.EffectiveGlyphStyle;
    }
#endif
}