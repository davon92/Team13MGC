// Assets/Scripts/ScoreDebugHUD.cs
using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;

public class ScoreDebugHUD : MonoBehaviour
{
    [Header("Refs (optional, will auto-find)")]
    [SerializeField] ScoreTracker        score;
    [SerializeField] KnobLaneController  knob;
    [SerializeField] ButtonLaneController buttons;
    [SerializeField] TMP_Text            label;

    [Header("Keys")]
    [SerializeField] Key toggleKey = Key.F1;
    [SerializeField] string gamepadToggleAction = "ToggleDebug"; // optional InputAction name if you want one

    [Header("Format")]
    [SerializeField] int digits = 8;

    bool visible = true;
    InputAction toggleAction;

    void Awake()
    {
        if (!label)   label   = GetComponent<TMP_Text>() ?? GetComponentInChildren<TMP_Text>(true);
        if (!score)   score   = FindFirstObjectByType<ScoreTracker>() ?? FindObjectOfType<ScoreTracker>();
        if (!knob)    knob    = FindFirstObjectByType<KnobLaneController>() ?? FindObjectOfType<KnobLaneController>();
        if (!buttons) buttons = FindFirstObjectByType<ButtonLaneController>() ?? FindObjectOfType<ButtonLaneController>();

        // Optional gamepad toggle action if present in a PlayerInput
        var pi = FindFirstObjectByType<PlayerInput>();
        if (pi && !string.IsNullOrEmpty(gamepadToggleAction))
            toggleAction = pi.actions.FindAction(gamepadToggleAction, false);
    }

    void OnEnable()
    {
        toggleAction?.Enable();
    }
    void OnDisable()
    {
        toggleAction?.Disable();
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
            visible = !visible;
        if (toggleAction != null && toggleAction.triggered)
            visible = !visible;

        if (!label) return;
        label.enabled = visible;
        if (!visible) return;

        // Safeties
        if (!score || !knob)
        {
            label.text = "Debug HUD: missing refs";
            return;
        }

        // EXPECTED per-frame add from trace:
        // expected = round( TracePerBeat * tick * beatsThisFrame * norm )
        int expectedTraceAdd = 0;
        if (score.DebugNormReady)
        {
            double raw = score.TracePerBeatRaw * knob.DebugLastTick * knob.DebugBeatsThisFrame;
            expectedTraceAdd = Mathf.RoundToInt( (float)(raw * score.DebugNorm) );
        }

        // ACTUAL last change (tap or trace)
        int actualDelta = score.DebugLastDelta;
        string tag      = score.DebugLastTag ?? "";

        // Totals for normalization sanity
        int taps = (buttons != null) ? buttons.TotalScorableTaps : -1;
        double beats = knob.TotalTraceBeats;

        // Build HUD
        label.text =
            $"SCORE: {score.Score.ToString($"D{digits}")}\n" +
            $"Δactual: {actualDelta}  [{tag}]\n" +
            $"Δexpect(trace): {expectedTraceAdd}  (tick {knob.DebugLastTick:0.00}  beats {knob.DebugBeatsThisFrame:0.000})\n" +
            $"NormReady: {score.DebugNormReady}  Norm: {score.DebugNorm:0.000000}\n" +
            $"Tot Taps: {taps}  Tot Trace Beats: {beats:0.00}\n" +
            $"Engaged: {knob.DebugEngagedRecent}  StickMag: {knob.DebugStickMag:0.00}  sampleΔ: {knob.DebugSampleDelta}";
    }
}
