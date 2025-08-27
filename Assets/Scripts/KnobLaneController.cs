using System;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// Rotary (Sound Voltex–style) knob controller.
/// Clockwise increases the knob; counter-clockwise decreases it.
/// Works with mouse circular drag OR gamepad left-stick circular motion.
public class KnobLaneController : MonoBehaviour
{
    [SerializeField] RhythmConductor conductor;
    [SerializeField] float angularDeadzoneDeg = 2f;

    [Header("Lane visuals")]
    [SerializeField] RectTransform stickParent;   // vertical lane rect (for target/player dots)
    [SerializeField] RectTransform targetDot;
    [SerializeField] public RectTransform playerDot;

    [Header("Rotary surface (mouse)")]
    [Tooltip("Area (usually a circular UI) used to measure mouse angle around its center.")]
    [SerializeField] RectTransform rotarySurface;
    [Tooltip("Require the pointer press to engage; otherwise hover rotates.")]
    [SerializeField] bool requirePressToRotate = true;
    [Tooltip("Minimum radius (UI units) from center to accept mouse movement as rotation.")]
    [SerializeField] float minMouseRadius = 15f;

    [Header("Gamepad")]
    [Tooltip("Left stick magnitude required to start tracking rotation.")]
    [SerializeField] float stickDeadzone = 0.25f;

    [Header("Sensitivity & direction")]
    [Tooltip("How many full turns move the knob from 0 → 1.0. (e.g., 0.75 = 3/4 rotation end-to-end)")]
    [SerializeField] float turnsForFullTravel = 0.75f;
    [Tooltip("If true, clockwise motion moves knob upward; else downward.")]
    [SerializeField] bool clockwiseIncreases = true;

    [Header("Grading")]
    [SerializeField] float perfectErr = 0.06f;
    [SerializeField] float greatErr   = 0.12f;
    [SerializeField] float goodErr    = 0.18f;

    [Header("Forgiveness")]
    [SerializeField, Tooltip("Multiply all windows. 1 = unchanged.")]
    float generosity = 1.35f;                // try 1.25–1.6

    [SerializeField, Tooltip("Extra forgiveness in pixels added to all windows.")]
    float forgivenessPx = 12f;               // try 8–20px

    [SerializeField, Tooltip("Optional: half of this rect’s height is added as forgiveness (eg the stick icon).")]
    RectTransform toleranceFrom;

    [Header("Grace (milliseconds)")]
    [SerializeField] float enterGraceMs = 60f;
    [SerializeField] float exitGraceMs  = 80f;

    public float GoodWindow => Mathf.Min(1f, goodErr * generosity + Add01());

    // Actions (instance-bound)
    [SerializeField] PlayerInput playerInput;
    InputAction leftStick;         // Vector2
    InputAction pointerPos;        // Vector2
    InputAction pointerPress;      // Button

    // State
    Rect laneRect; bool haveLane;
    float player01;                // current knob value 0..1
    float prevMouseAngle; bool mouseTracking;
    float prevStickAngle; bool stickTracking;

    int enterGraceSamples, exitGraceSamples;

    // Chart spans & scoring (unchanged API)
    public System.Action<Judgement> OnJudged;
    public Action<float, float> OnTraceSpanScored;
    readonly Queue<RhythmTypes.KnobSpan> spans = new();
    RhythmTypes.KnobSpan? active;
    float sumScore, maxScore;
    
    bool headJudged = false;

    float Add01()
    {
        if (!haveLane) return 0f;
        float h = Mathf.Max(1f, laneRect.height);
        float px = forgivenessPx;
        if (toleranceFrom) px = Mathf.Max(px, toleranceFrom.rect.height * 0.5f);
        return Mathf.Clamp01(px / h);
    }

    // Returns absolute error (0..1) at the *visual* time; false if no active span.
    public bool TryGetVisualError(out float err)
    {
        err = 1f;
        if (!active.HasValue) return false;
        float target01V = Mathf.Clamp01(active.Value.targetAtSample(conductor.NowForVisual));
        err = Mathf.Abs(target01V - player01);
        return true;
    }

    public void SetTurnsForFullTravel(float turns)
    {
        turnsForFullTravel = Mathf.Max(0.05f, turns);
    }

    Judgement GradeByError(float absErr)
    {
        if (absErr <= perfectErr) return Judgement.Perfect;
        if (absErr <= greatErr)   return Judgement.Great;
        if (absErr <= goodErr)    return Judgement.Good;
        return Judgement.Miss;
    }
    
    void Awake()
    {
        if (!playerInput) playerInput = FindFirstObjectByType<PlayerInput>();
        var actions = playerInput ? playerInput.actions : null;

        leftStick    = actions?.FindAction("LeftStick", true);
        pointerPos   = actions?.FindAction("KnobPointer", false);
        pointerPress = actions?.FindAction("KnobPointerPress", false);
    }

    void OnEnable()
    {
        leftStick?.Enable();
        pointerPos?.Enable();
        pointerPress?.Enable();
    }
    void OnDisable()
    {
        leftStick?.Disable();
        pointerPos?.Disable();
        pointerPress?.Disable();
    }

    void Start()
    {
        if (stickParent) { laneRect = stickParent.rect; haveLane = true; }
        if (targetDot) targetDot.gameObject.SetActive(false);

        int sr = conductor.SampleRate;
        enterGraceSamples = Mathf.RoundToInt(enterGraceMs * 0.001f * sr);
        exitGraceSamples  = Mathf.RoundToInt(exitGraceMs  * 0.001f * sr);
    }

    // Called by KoreographyChartLoader
    public void LoadChart(IEnumerable<RhythmTypes.KnobSpan> segments)
    {
        spans.Clear();
        foreach (var s in segments) spans.Enqueue(s);
        active = null;
        sumScore = maxScore = 0f;
        if (targetDot) targetDot.gameObject.SetActive(false);
    }

    void Update()
    {
        if (conductor.IsFinished)
        {
            // Hide target and keep player marker steady; no new spans.
            if (targetDot) targetDot.gameObject.SetActive(false);
            DrawPlayer(player01);
            return;
        }

        // 1) Read rotary delta (turns): + = clockwise, − = counter-clockwise
        float deltaTurns = ReadRotaryTurnsThisFrame();

        // 2) Integrate to 0..1 space
        float sign = clockwiseIncreases ? 1f : -1f;
        if (turnsForFullTravel <= 0f) turnsForFullTravel = 1f;
        player01 = Mathf.Clamp01(player01 + sign * (deltaTurns / turnsForFullTravel));

        int nowV = conductor.NowForVisual;

        // Activate new span as it comes into the visual window
        if (!active.HasValue && spans.Count > 0 && spans.Peek().startSample <= nowV)
        {
            active = spans.Dequeue();
            sumScore = 0f; maxScore = 0f;
            headJudged = false;                          // <-- add this
            if (targetDot) targetDot.gameObject.SetActive(true);
        }

        // Early visuals when no span active
        if (!active.HasValue)
        {
            DrawPlayer(player01);
            return;
        }

        var s = active.Value;
        int nowH = conductor.NowForHit;

        if (!headJudged && nowH >= s.startSample - enterGraceSamples)
        {
            // Measure instantaneous error on the HIT timeline
            float target01H = Mathf.Clamp01(s.targetAtSample(nowH));
            float err = Mathf.Abs(target01H - player01);

            // Use the same forgiveness you use for ticks
            float add = Add01();
            float pWin = Mathf.Min(1f, perfectErr * generosity + add);
            float gWin = Mathf.Min(1f, greatErr   * generosity + add);
            float bWin = Mathf.Min(1f, goodErr    * generosity + add);

            Judgement j = (err <= pWin) ? Judgement.Perfect
                : (err <= gWin) ? Judgement.Great
                : (err <= bWin) ? Judgement.Good
                : Judgement.Miss;

            OnJudged?.Invoke(j);
            headJudged = true; // lock so we only judge once at the head
        }

        // Draw target & player
        float target01V = Mathf.Clamp01(s.targetAtSample(nowV));
        DrawTarget(target01V);
        DrawPlayer(player01);

        // Accumulate accuracy inside hit window
        if (nowH >= s.startSample - enterGraceSamples && nowH <= s.endSample)
        {
            float target01H = Mathf.Clamp01(s.targetAtSample(nowH));

            float add = Add01();
            float pWin = Mathf.Min(1f, perfectErr * generosity + add);
            float gWin = Mathf.Min(1f, greatErr   * generosity + add);
            float bWin = Mathf.Min(1f, goodErr    * generosity + add);

            float err = Mathf.Abs(target01H - player01);
            float tick = (err <= pWin) ? 1f
                : (err <= gWin) ? 0.7f
                : (err <= bWin) ? 0.4f
                : 0f;

            sumScore += tick; maxScore += 1f;
            
            if (tick > 0f && TryGetComponent<ScoreTracker>(out var tracker))
            {
                tracker.KnobTraceComboPulse(true, Time.unscaledDeltaTime);
            }
        }
        
        // At end + small grace: clear the span (only finalize if head never judged)
        if (nowH > s.endSample + exitGraceSamples)
        {
            if (!headJudged) FinalizeSpan(sumScore, maxScore); // fallback (rare)
            OnTraceSpanScored?.Invoke(sumScore, maxScore);
            active = null;
            headJudged = false;
            if (targetDot) targetDot.gameObject.SetActive(false);
            return;
        }
        

    }

    // ----- Rotary math -----
    float ReadRotaryTurnsThisFrame()
    {
        float turns = 0f;

        // Mouse circular drag
        if (pointerPos != null && rotarySurface)
        {
            bool engage = !requirePressToRotate || (pointerPress != null && pointerPress.IsPressed());
            if (engage)
            {
                Vector2 screen = pointerPos.ReadValue<Vector2>();
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rotarySurface, screen, null, out var local))
                {
                    // center of the rect
                    var rr = rotarySurface.rect;
                    Vector2 center = new((rr.xMin + rr.xMax) * 0.5f, (rr.yMin + rr.yMax) * 0.5f);
                    Vector2 dir = local - center;
                    float r = dir.magnitude;

                    if (r >= minMouseRadius)
                    {
                        float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg; // -180..180
                        if (!mouseTracking) { prevMouseAngle = ang; mouseTracking = true; }
                        else
                        {
                            float d = Mathf.DeltaAngle(prevMouseAngle, ang);
                            if (Mathf.Abs(d) < angularDeadzoneDeg) d = 0f;   // ignore tiny twitches
                            prevMouseAngle = ang;
                            turns += d / 360f;
                        }
                    }
                }
            }
            else mouseTracking = false;
        }

        // Gamepad circular motion
        if (leftStick != null)
        {
            Vector2 v = leftStick.ReadValue<Vector2>();
            if (v.magnitude >= stickDeadzone)
            {
                float ang = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
                if (!stickTracking) { prevStickAngle = ang; stickTracking = true; }
                else
                {
                    float d = Mathf.DeltaAngle(prevStickAngle, ang);
                    if (Mathf.Abs(d) < angularDeadzoneDeg) d = 0f;
                    prevStickAngle = ang;
                    turns += d / 360f;
                }
            }
            else stickTracking = false;
        }

        return turns;
    }

    public void ClearAll()
    {
        spans.Clear();
        active = null;
        sumScore = maxScore = 0f;
        if (targetDot) targetDot.gameObject.SetActive(false);
    }

    // ----- Drawing helpers -----
    void DrawPlayer(float t01)
    {
        if (!haveLane || !playerDot) return;
        var p = playerDot.anchoredPosition;
        p.y = Mathf.Lerp(laneRect.yMin, laneRect.yMax, Mathf.Clamp01(t01));
        playerDot.anchoredPosition = p;
    }

    void DrawTarget(float t01)
    {
        if (!haveLane || !targetDot) return;
        var p = targetDot.anchoredPosition;
        p.y = Mathf.Lerp(laneRect.yMin, laneRect.yMax, Mathf.Clamp01(t01));
        targetDot.anchoredPosition = p;
    }

    // ----- Judgement finalize -----
    void FinalizeSpan(float sum, float max)
    {
        float r = (max > 0f) ? (sum / max) : 0f;
        Judgement j = (r >= 0.90f) ? Judgement.Perfect
                    : (r >= 0.75f) ? Judgement.Great
                    : (r >= 0.55f) ? Judgement.Good
                    : Judgement.Miss;
        OnJudged?.Invoke(j);
        Debug.Log($"[Knob] {j} (ratio {r:0.00})");
    }
}
