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
    [SerializeField] RectTransform rotarySurface;
    [SerializeField] bool requirePressToRotate = true;
    [SerializeField] float minMouseRadius = 15f;

    [Header("Gamepad")]
    [SerializeField] float stickDeadzone = 0.25f;

    [Header("Sensitivity & direction")]
    [SerializeField] float turnsForFullTravel = 0.75f;
    [SerializeField] bool clockwiseIncreases = true;

    [Header("Grading")]
    [SerializeField] float perfectErr = 0.06f;
    [SerializeField] float greatErr   = 0.12f;
    [SerializeField] float goodErr    = 0.18f;

    [Header("Forgiveness")]
    [SerializeField] float generosity = 1.35f;      // multiply windows
    [SerializeField] float forgivenessPx = 12f;     // add pixels to windows
    [SerializeField] RectTransform toleranceFrom;

    [Header("Grace (milliseconds)")]
    [SerializeField] float enterGraceMs = 60f;
    [SerializeField] float exitGraceMs  = 80f;
    int _prevNowH;
    public float GoodWindow => Mathf.Min(1f, goodErr * generosity + Add01());
    
    [SerializeField] ScoreTracker scoreTracker;

    // Actions (instance-bound)
    [SerializeField] PlayerInput playerInput;
    InputAction leftStick;         // Vector2
    InputAction pointerPos;        // Vector2
    InputAction pointerPress;      // Button
    
 
    [SerializeField, Tooltip("Min knob movement in 0..1 to count as engaged this frame.")]
    float minDelta01ForEngagement = 0.0025f;
    float _prevPlayer01;

    // State
    Rect laneRect; bool haveLane;
    float player01;                // current knob value 0..1
    float prevMouseAngle; bool mouseTracking;
    float prevStickAngle; bool stickTracking;

    int enterGraceSamples, exitGraceSamples;

    // Chart spans & scoring
    public System.Action<Judgement> OnJudged;
    public Action<float, float> OnTraceSpanScored;
    readonly Queue<RhythmTypes.KnobSpan> spans = new();
    RhythmTypes.KnobSpan? active;
    float sumScore, maxScore;
    bool headJudged = false;
    
    // --- DEBUG: per-frame readouts ---
    public float DebugLastTick { get; private set; }          // 0, 0.4, 0.7, 1
    public float DebugBeatsThisFrame { get; private set; }    // beats advanced this frame on hit timeline
    public bool  DebugEngagedRecent  { get; private set; }    // true when movement recently happened
    public float DebugStickMag       { get; private set; }    // magnitude of the left stick
    public int   DebugSampleDelta    { get; private set; }    // NowForHit - _prevNowH this frame


    [SerializeField, Tooltip("How long movement stays 'hot' (seconds).")]
    float engagedHoldSeconds = 0.15f;

    float _engagedTimer; // counts down after movement

    float Add01()
    {
        if (!haveLane) return 0f;
        float px = forgivenessPx;         // keep this modest in the Inspector (e.g., 6–12 px)
        float h  = Mathf.Max(1f, laneRect.height);
        return Mathf.Clamp01(px / h);     // typically < 0.1
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
    
    bool EngagedByMovementThisFrame(float delta01, float deltaTurns)
    {
        // Count as engaged only if knob position changed a bit in 0..1 or you actually rotated
        return Mathf.Abs(delta01) >= minDelta01ForEngagement || Mathf.Abs(deltaTurns) > 0.0001f;
    }
    
    // <<< Used by ScoreTracker normalization >>> 
    public double TotalTraceBeats
    {
        get
        {
            if (spans == null || spans.Count == 0) return 0;
            double spb = System.Math.Max(1, conductor.SamplesPerBeat);
            double sum = 0;
            foreach (var s in spans)
                sum += System.Math.Max(0, (s.endSample - s.startSample) / spb); // <-- startSample/endSample
            return sum;
        }
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

        if (!scoreTracker) scoreTracker = FindFirstObjectByType<ScoreTracker>() ?? FindObjectOfType<ScoreTracker>();
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
        _prevNowH = conductor.NowForHit;
        _prevPlayer01 = 0f;
        _engagedTimer = 0f;

        if (stickParent) { laneRect = stickParent.rect; haveLane = true; }
        if (targetDot) targetDot.gameObject.SetActive(false);

        // (optional) make lane slightly wider than knob UI
        if (stickParent && playerDot)
        {
            var size = stickParent.sizeDelta;
            float want = Mathf.Max(size.x, playerDot.rect.width * 1.25f);
            stickParent.sizeDelta = new Vector2(want, size.y);
            laneRect = stickParent.rect;
        }

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
        headJudged = false;
        if (targetDot) targetDot.gameObject.SetActive(false);
    }

    void Update()
    {
        if (conductor.IsFinished)
        {
            if (targetDot) targetDot.gameObject.SetActive(false);
            DrawPlayer(player01);
            _prevNowH = conductor.NowForHit;
            _prevPlayer01 = player01;
            _engagedTimer = 0f;
            return;
        }

        // 1) Read delta turns & integrate to 0..1
        float deltaTurns = ReadRotaryTurnsThisFrame();

        float sign = clockwiseIncreases ? 1f : -1f;
        if (turnsForFullTravel <= 0f) turnsForFullTravel = 1f;

        float before01 = player01;
        player01 = Mathf.Clamp01(player01 + sign * (deltaTurns / turnsForFullTravel));
        float delta01 = player01 - before01;

        // movement engagement: movement now keeps a short "hot" timer
        bool movedNow = Mathf.Abs(delta01) >= minDelta01ForEngagement || Mathf.Abs(deltaTurns) > 0.0001f;
        if (movedNow) _engagedTimer = engagedHoldSeconds;
        else _engagedTimer = Mathf.Max(0f, _engagedTimer - Time.unscaledDeltaTime);
        bool engaged = _engagedTimer > 0f;

        int nowV = conductor.NowForVisual;

        // 2) Activate span at visual time
        if (!active.HasValue && spans.Count > 0 && spans.Peek().startSample <= nowV)
        {
            active = spans.Dequeue();
            sumScore = 0f; maxScore = 0f;
            headJudged = false;
            if (targetDot) targetDot.gameObject.SetActive(true);
        }

        if (!active.HasValue)
        {
            DrawPlayer(player01);
            _prevNowH = conductor.NowForHit;
            _prevPlayer01 = player01;
            return;
        }

        var s = active.Value;
        int nowH = conductor.NowForHit;

        // 3) Head judgement once (with grace)
        if (!headJudged && nowH >= s.startSample - enterGraceSamples)
        {
            float target01H = Mathf.Clamp01(s.targetAtSample(nowH));
            float err = Mathf.Abs(target01H - player01);

            float add = Add01();
            float pWin = Mathf.Min(1f, perfectErr * generosity + add);
            float gWin = Mathf.Min(1f, greatErr   * generosity + add);
            float bWin = Mathf.Min(1f, goodErr    * generosity + add);

            Judgement j = (err <= pWin) ? Judgement.Perfect
                       : (err <= gWin) ? Judgement.Great
                       : (err <= bWin) ? Judgement.Good
                       : Judgement.Miss;

            OnJudged?.Invoke(j);
            headJudged = true;
        }

        // 4) Draw target & player
        float target01V = Mathf.Clamp01(s.targetAtSample(nowV));
        DrawTarget(target01V);
        DrawPlayer(player01);

        // 5) Per-frame trace ticks while inside scoring window
        if (nowH >= s.startSample - enterGraceSamples && nowH <= s.endSample)
        {
            float target01H = Mathf.Clamp01(s.targetAtSample(nowH));

            float add = Add01();
            float pWin = Mathf.Min(1f, perfectErr * generosity + add);
            float gWin = Mathf.Min(1f, greatErr   * generosity + add);
            float bWin = Mathf.Min(1f, goodErr    * generosity + add);

            float err  = Mathf.Abs(target01H - player01);
            float tick = (err <= pWin) ? 1f
                : (err <= gWin) ? 0.7f
                : (err <= bWin) ? 0.4f
                : 0f;

            // local accumulation (optional, for your end-of-span judgement)
            sumScore += tick;
            maxScore += 1f;

            // ---------- DEBUG CAPTURE (put these BEFORE awarding points) ----------
            DebugLastTick = tick;

            int sampleDelta = Mathf.Max(0, conductor.NowForHit - _prevNowH);
            DebugSampleDelta = sampleDelta;

            float beatsThisFrame = sampleDelta / (float)conductor.SamplesPerBeat;
            DebugBeatsThisFrame = beatsThisFrame;

            DebugEngagedRecent = engaged;  // your movement gate from earlier in Update()

            // ---------- AWARD POINTS (only when engaged & tick > 0 & time advanced) ----------
            if (scoreTracker && engaged && tick > 0f && beatsThisFrame > 0f)
            {
                scoreTracker.KnobTraceTick(tick, beatsThisFrame);
            }
        }


        // 6) End of span (with exit grace)
        if (nowH > s.endSample + exitGraceSamples)
        {
            if (!headJudged) FinalizeSpan(sumScore, maxScore);
            OnTraceSpanScored?.Invoke(sumScore, maxScore);
            active = null;
            headJudged = false;
            if (targetDot) targetDot.gameObject.SetActive(false);
            _prevNowH = conductor.NowForHit;
            _prevPlayer01 = player01;
            return;
        }

        // keep sample delta & movement trackers fresh
        _prevNowH = conductor.NowForHit;
        _prevPlayer01 = player01;
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
                            if (Mathf.Abs(d) < angularDeadzoneDeg) d = 0f;
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
            DebugStickMag = v.magnitude;
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

    bool IsEngagedThisFrame()
    {
        // Mouse held down on the rotary surface?
        bool mouse = pointerPress != null && pointerPress.IsPressed();

        // Stick pushed beyond deadzone?
        bool stick = false;
        if (leftStick != null)
        {
            var v = leftStick.ReadValue<Vector2>();
            stick = v.magnitude >= stickDeadzone;
        }

        return mouse || stick;
    }

    
    public void ClearAll()
    {
        spans.Clear();
        active = null;
        sumScore = maxScore = 0f;
        headJudged = false;
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
