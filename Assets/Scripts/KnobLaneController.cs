using System;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using UnityEngine.UI;

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
    
    [Header("Hint Icon (optional)")]
    [SerializeField] Image knobIcon; // NEW: e.g., a small icon near the lane
    
    [SerializeField] ScoreTracker scoreTracker;
    public int TotalTraceHeads { get; private set; }
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
    
    // --- Mouse absolute (Trombone-like) ---
    [Header("Mouse Absolute (Trombone style)")]
    [SerializeField] bool  mouseAbsolute = true;           // use screen Y as target 0..1
    [SerializeField] bool  mouseAbsRequirePress = false;   // only while LMB down (optional)
    [SerializeField, Range(0.0f, 30f)]
    float mouseAbsLerpPerSec = 12f;                        // how quickly we follow target (0=instant)
    [SerializeField] bool  invertMouseY = false;           // if you want up=down instead

    
    // --- DEBUG: per-frame readouts ---
    public float DebugLastTick { get; private set; }          // 0, 0.4, 0.7, 1
    public float DebugBeatsThisFrame { get; private set; }    // beats advanced this frame on hit timeline
    public bool  DebugEngagedRecent  { get; private set; }    // true when movement recently happened
    public float DebugStickMag       { get; private set; }    // magnitude of the left stick
    public int   DebugSampleDelta    { get; private set; }    // NowForHit - _prevNowH this frame
    
    
    // --------- Assist / Magnet (SDVX-like) ----------
    [Header("Magnet (assist)")]
    [SerializeField] bool  magnetEnabled          = true;
    [SerializeField, Range(0f, 0.25f)]
    float magnetRadius         = 0.06f;   // how close (0..1) you need to be before magnet starts helping
    [SerializeField, Range(0f, 1f)]
    float magnetGain           = 0.50f;   // fraction of remaining error corrected per beat (inside radius)
    [SerializeField, Range(0f, 1f)]
    float magnetMax01PerBeat   = 0.20f;   // cap on how much the magnet can move per beat
    [SerializeField] bool  magnetOnlyWhenEngaged  = true; // only help when the player is actively moving

    
    // -------- Asymmetric magnet (approach vs. leave) --------
    [SerializeField] bool  magnetAsymmetric      = true;
    [SerializeField, Range(0f, 1f)] float approachGain   = 0.65f; // stronger pull when closing in
    [SerializeField, Range(0f, 1f)] float leaveGain      = 0.35f; // weaker pull when drifting away
    [SerializeField, Range(0f, 0.25f)] float approachRadius = 0.08f;
    [SerializeField, Range(0f, 0.25f)] float leaveRadius    = 0.05f;

    // Optional micro-snap when super close (prevents tiny oscillation)
    [SerializeField] bool  magnetHysteresis      = true;
    [SerializeField, Range(0f, 0.12f)] float innerSnapRadius = 0.02f; // inside this, add extra snap
    [SerializeField, Range(0f, 1f)] float innerSnapGain     = 0.85f;

    // -------- Corner boost --------
    [SerializeField] bool  cornerBoost           = true;
    [SerializeField, Range(0.0f, 3.0f)] float cornerGainMul   = 1.6f;  // multiply gain near corners
    [SerializeField, Range(0.5f, 2.0f)] float cornerRadiusMul = 1.35f; // enlarge radius near corners
    [SerializeField, Range(0.00f, 1.00f)] float slopePerBeatThresh = 0.30f; // |d target01 / d beat|
    [SerializeField, Range(0.00f, 1.00f)] float curvatureThresh    = 0.20f; // |2nd derivative per beat^2|

    // internals for asymmetry detection
    float _prevErr = 0f;
    
    // -------- Lock-on follow (SDVX-like) --------
    [Header("Lock-on")]
    [SerializeField] bool  lockOnEnabled = true;
    [SerializeField, Range(0f, 0.15f)] float lockAcquireRadius = 0.06f;  // must be this close to snap on
    [SerializeField, Range(0f, 0.40f)] float lockSnapPerBeat  = 0.25f;   // max 0..1 movement per beat while locked
    [SerializeField, Range(0f, 0.25f)] float lockBreakRadius  = 0.12f;   // if error grows past this, unlock
    [SerializeField] float lockIdleBreakMs = 250f;                         // unlock if no movement for this long
    [SerializeField] float lockReverseGraceMs = 120f;                      // grace when laser flips direction
    [SerializeField] bool  lockHardStick = true;        // <— NEW: hard follow when direction is correct


// runtime lock state
    bool  _locked;
    int   _lockRequiredDir;      // -1 down, +1 up, 0 flat/any
    float _lockReverseGraceT;    // seconds remaining
    float _lockIdleMs;           // accumulated idle time
    
    
    // --------- Trace miss -> break combo ----------
    [Header("Trace miss → break combo")]
    [SerializeField] float breakComboAfterMs      = 120f; // how long off-path before combo breaks
    [SerializeField] bool  missRequiresEngagement = false; // if true, only break while input is engaged

// --------- Visual scale (triangle sits inside lane) ----------
    [Header("Visual scaling")]
    [SerializeField] float playerDotScale         = 0.85f; // shrink the knob/triangle a bit
    [SerializeField] float laneToPlayerWidthRatio = 1.40f; // make lane at least this much wider than the triangle

// --------- Internals ----------
    float _offPathTimerMs;  // accumulates while off-path inside a span


    [SerializeField, Tooltip("How long movement stays 'hot' (seconds).")]
    float engagedHoldSeconds = 0.15f;

    float _engagedTimer; // counts down after movement
    
    bool _useRotaryFrozen;
    bool UseRotaryNow() => _useRotaryFrozen;
    float Add01()
    {
        if (!haveLane) return 0f;
        float px = forgivenessPx;            // set 6–12 px in the Inspector
        float h  = Mathf.Max(1f, laneRect.height);
        return Mathf.Clamp01(px / h);        // usually < 0.1
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
    
    void Awake() {
        if (!playerInput) playerInput = FindFirstObjectByType<PlayerInput>();
        var actions = playerInput ? playerInput.actions : null;

        leftStick    = actions?.FindAction("LeftStick",         true);
        pointerPos   = actions?.FindAction("KnobPointer",       false);
        pointerPress = actions?.FindAction("KnobPointerPress",  false);

        if (!scoreTracker) scoreTracker = FindFirstObjectByType<ScoreTracker>() ?? FindObjectOfType<ScoreTracker>();
    }

    void OnEnable()
    {
        leftStick?.Enable();
        pointerPos?.Enable();
        pointerPress?.Enable();
        
        var style  = SettingsService.EffectiveGlyphStyle;
        var glyphs = GlyphLibrary.Get(style);
        if (knobIcon && glyphs && glyphs.knobIcon)
            knobIcon.sprite = glyphs.knobIcon;
    }
    void OnDisable()
    {
        leftStick?.Disable();
        pointerPos?.Disable();
        pointerPress?.Disable();
    }
    
    bool TryGetAbsoluteMouseTarget(out float target01) {
        target01 = 0.5f;
        if (pointerPos == null) return false;
        var screen = pointerPos.ReadValue<Vector2>();

        // 0 at bottom, 1 at top (Canvas overlay or not—screen height works fine)
        float y01 = Mathf.Clamp01(screen.y / Mathf.Max(1f, (float)Screen.height));
        if (invertMouseY) y01 = 1f - y01;

        // optional gate: only when mouse held
        if (mouseAbsRequirePress && !(pointerPress != null && pointerPress.IsPressed()))
            return false;

        target01 = y01;
        return true;
    }
    
    void OnStyleChanged(InputGlyphStyle _)
    {
        if (!knobIcon) return;
        var g = GlyphLibrary.Current;
        if (g && g.knobIcon) knobIcon.sprite = g.knobIcon;  // Mouse for KBM, LeftStick for pads
    }

    void Start()
    {
        _useRotaryFrozen = (SettingsService.EffectiveGlyphStyle != InputGlyphStyle.KeyboardMouse);
        _prevNowH      = conductor.NowForHit;
        _prevPlayer01  = 0f;
        _engagedTimer  = 0f;
        _offPathTimerMs = 0f;

        if (stickParent) { laneRect = stickParent.rect; haveLane = true; }
        if (targetDot) targetDot.gameObject.SetActive(false);

        // Visual adjustments so the triangle fits comfortably inside the lane
        if (playerDot)
            playerDot.localScale = new Vector3(playerDotScale, playerDotScale, 1f);

        if (stickParent && playerDot)
        {
            var size    = stickParent.sizeDelta;
            float triW  = playerDot.rect.width * playerDot.localScale.x;
            float wantW = Mathf.Max(size.x, triW * laneToPlayerWidthRatio);
            stickParent.sizeDelta = new Vector2(wantW, size.y);
            laneRect = stickParent.rect; // refresh cache
        }

        int sr = conductor.SampleRate;
        enterGraceSamples = Mathf.RoundToInt(enterGraceMs * 0.001f * sr);
        exitGraceSamples  = Mathf.RoundToInt(exitGraceMs  * 0.001f * sr);

        if (!scoreTracker) scoreTracker = FindFirstObjectByType<ScoreTracker>() ?? FindObjectOfType<ScoreTracker>();
    }



    // Called by KoreographyChartLoader
    public void LoadChart(IEnumerable<RhythmTypes.KnobSpan> segments)
    {
        spans.Clear();
        TotalTraceHeads = 0;

        foreach (var s in segments)
        {
            spans.Enqueue(s);
            TotalTraceHeads++;  // one head judgement per span
        }

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
            _offPathTimerMs = 0f;
            return;
        }

// --- 1) Read input and update player01 ---
        float deltaTurns = 0f;                 // rotary-only path
        float before01   = player01;

        if (UseRotaryNow())
        {
            // Gamepad rotary (left stick circular motion)
            deltaTurns = ReadRotaryTurnsThisFrame();
            float sign = clockwiseIncreases ? +1f : -1f;
            if (turnsForFullTravel <= 0f) turnsForFullTravel = 1f;
            player01 = Mathf.Clamp01(player01 + sign * (deltaTurns / turnsForFullTravel));
        }
        else
        {
            // Mouse absolute Y (whole screen). No scroll wheel.
            if (TryGetAbsoluteMouseTarget(out var target01)) {
                float t = 1f - Mathf.Exp(-mouseAbsLerpPerSec * Time.unscaledDeltaTime);
                player01 = Mathf.Lerp(player01, target01, t);
            }
        }

        float delta01 = player01 - before01;

    // movement gate (keeps “engaged” hot for a short time)
        bool movedNow = EngagedByMovementThisFrame(delta01, deltaTurns);
        if (movedNow) _engagedTimer = engagedHoldSeconds;
        else          _engagedTimer = Mathf.Max(0f, _engagedTimer - Time.unscaledDeltaTime);
        bool engaged = _engagedTimer > 0f;

        int nowV = conductor.NowForVisual;

        // 2) Activate span when it enters the visual window
        if (!active.HasValue && spans.Count > 0)
        {
            int start = spans.Peek().startSample;
            if (start <= nowV || start <= (conductor.NowForHit + enterGraceSamples))
            {
                active = spans.Dequeue();
                sumScore = 0f; maxScore = 0f;
                headJudged = false;
                _offPathTimerMs = 0f;
                if (targetDot) targetDot.gameObject.SetActive(true);
            }
        }

        // 3) No span -> draw & exit
        if (!active.HasValue)
        {
            DrawPlayer(player01);
            _prevNowH = conductor.NowForHit;
            _prevPlayer01 = player01;
            return;
        }

        var s   = active.Value;
        int nowH = conductor.NowForHit;
        
        // allow pre-start acquisition within grace
        if (!_locked && lockOnEnabled && engaged && active.HasValue)
        {
            int start = active.Value.startSample;
            if (nowH >= start - enterGraceSamples)
            {
                float target01H = Mathf.Clamp01(active.Value.targetAtSample(nowH));
                if (Mathf.Abs(target01H - player01) <= lockAcquireRadius)
                    _locked = true;
            }
        }
        
        // 4) Lock-on follow (with required direction). If not locked (or can’t lock), fall back to magnet.
        GetTargetDerivatives(nowH, s, out float slopeB, out _);          // you already have this helper
        int requiredDir = RequiredDirFromSlope(slopeB);

        // Determine input direction in 0..1 space for this frame:
        float turn01delta = (clockwiseIncreases ? +1f : -1f) * (deltaTurns / Mathf.Max(0.0001f, turnsForFullTravel));
        int   inputDir01  = Mathf.Abs(turn01delta) >= minDelta01ForEngagement ? (turn01delta > 0f ? +1 : -1) : 0;

        // Try to acquire lock when close enough and engaged
        float target01H_forAcquire = Mathf.Clamp01(s.targetAtSample(nowH));
        if (!_locked && lockOnEnabled && engaged && Mathf.Abs(target01H_forAcquire - player01) <= lockAcquireRadius)
            _locked = true;

        if (_locked && lockOnEnabled)
        {
            var res = LockFollow(player01, nowH, s, inputDir01, requiredDir);
            _locked   = res.keepLocked;
            player01  = res.newPlayer01;
        }
        else
        {
            // simple magnet as a fallback when not locked
            player01 = ApplyMagnetAssist(player01, nowH, s, engaged);
            _lockIdleMs = 0f; // reset idle while not locked
        }

        // 5) One-time head judgement (with grace)
        if (!headJudged && nowH >= s.startSample - enterGraceSamples)
        {
            float target01H = Mathf.Clamp01(s.targetAtSample(nowH));
            float err = Mathf.Abs(target01H - player01);

            float add = Add01(); // small pixel-derived forgiveness
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

        // 6) Draw target & player
        float target01V = Mathf.Clamp01(s.targetAtSample(nowV));
        DrawTarget(target01V);
        DrawPlayer(player01);

        // 7) Per-frame trace ticks & combo break logic while inside scoring window
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

            // local accumulation (optional for end-of-span summary)
            sumScore += tick; 
            maxScore += 1f;

            // ---- NEW: break combo if you leave the path for a short time ----
            if (tick <= 0f && (!missRequiresEngagement || engaged))
            {
                _offPathTimerMs += Time.unscaledDeltaTime * 1000f;
                if (_offPathTimerMs >= breakComboAfterMs)
                {
                    OnJudged?.Invoke(Judgement.Miss); // ScoreTracker will BreakCombo + lock trace combo
                    _offPathTimerMs = 0f;             // throttle: one break per window
                }
            }
            else
            {
                _offPathTimerMs = 0f; // reset once you’re back on path
            }

            // ---- Award trace points only when engaged & tick > 0 & time really advanced ----
            if (scoreTracker && engaged && tick > 0f)
            {
                int sampleDelta = Mathf.Max(0, conductor.NowForHit - _prevNowH);
                float beatsThisFrame = sampleDelta / (float)conductor.SamplesPerBeat;
                if (beatsThisFrame > 0f)
                    scoreTracker.KnobTraceTick(tick, beatsThisFrame);
            }
        }

        // 8) End of span (with exit grace)
        if (nowH > s.endSample + exitGraceSamples)
        {
            if (!headJudged) FinalizeSpan(sumScore, maxScore);
            OnTraceSpanScored?.Invoke(sumScore, maxScore);
            active = null;
            _locked = false;
            _lockIdleMs = 0f;
            _lockReverseGraceT = 0f;
            _lockRequiredDir = 0;
            headJudged = false;
            if (targetDot) targetDot.gameObject.SetActive(false);
            _prevNowH = conductor.NowForHit;
            _prevPlayer01 = player01;
            _offPathTimerMs = 0f;
            return;
        }

        _prevNowH = conductor.NowForHit;
        _prevPlayer01 = player01;
    }
   
    // Compute required direction from the laser slope: +1 up, -1 down, 0 = flat/any.
    int RequiredDirFromSlope(float slopePerBeat, float thresh = 0.02f)
    {
        if (Mathf.Abs(slopePerBeat) < thresh) return 0;
        return slopePerBeat > 0f ? +1 : -1;
    }
    
    (bool keepLocked, float newPlayer01) LockFollow(float player01, int nowH, RhythmTypes.KnobSpan s, int inputDir01, int requiredDir)
    {
        // If the laser reverses direction, allow brief grace to change input.
        if (requiredDir != _lockRequiredDir)
        {
            _lockRequiredDir = requiredDir;
            _lockReverseGraceT = lockReverseGraceMs * 0.001f; // seconds
        }
        else
        {
            _lockReverseGraceT = Mathf.Max(0f, _lockReverseGraceT - Time.unscaledDeltaTime);
        }

        // Idle timer (need some movement occasionally)
        if (inputDir01 == 0) _lockIdleMs += Time.unscaledDeltaTime * 1000f;
        else                 _lockIdleMs = 0f;

        if (_lockIdleMs >= lockIdleBreakMs) return (false, player01);

        // If a direction is required and we're moving the wrong way after grace, unlock
        if (requiredDir != 0 && inputDir01 != 0 && _lockReverseGraceT <= 0f && inputDir01 != requiredDir)
            return (false, player01);

        float target01 = Mathf.Clamp01(s.targetAtSample(nowH));

        if (lockHardStick)
        {
            // HARD: if direction is acceptable (or within grace), snap exactly to the laser.
            if (requiredDir == 0 || inputDir01 == requiredDir || _lockReverseGraceT > 0f)
                return (true, target01);

            // if direction is unknown (flat) or you’re not yet turning the right way, ease a bit
            float beats = Time.unscaledDeltaTime * (conductor.Bpm / 60f);
            float step  = lockSnapPerBeat * beats;
            float out01 = Mathf.MoveTowards(player01, target01, step);
            return (true, out01);
        }
        else
        {
            // SOFT: previous behavior (speed-capped follower)
            float beats = Time.unscaledDeltaTime * (conductor.Bpm / 60f);
            float step  = lockSnapPerBeat * beats;
            float out01 = Mathf.MoveTowards(player01, target01, step);
            return (true, out01);
        }
    }
    // ----- Rotary math -----
    float ReadRotaryTurnsThisFrame()
    {
        float turns = 0f;
        
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

        // reset lock state too
        _locked = false;
        _lockIdleMs = 0f;
        _lockReverseGraceT = 0f;
        _lockRequiredDir = 0;
    }

    // ----- Drawing helpers -----
    void DrawPlayer(float t01) {
        if (!haveLane || !playerDot) return;
        var p = playerDot.anchoredPosition;
        p.y = Mathf.Lerp(laneRect.yMin, laneRect.yMax, Mathf.Clamp01(t01));
        playerDot.anchoredPosition = p;
    }
    void DrawTarget(float t01) {
        if (!haveLane || !targetDot) return;
        var p = targetDot.anchoredPosition;
        p.y = Mathf.Lerp(laneRect.yMin, laneRect.yMax, Mathf.Clamp01(t01));
        targetDot.anchoredPosition = p;
    }
    void OnRectTransformDimensionsChange()
    {
        if (stickParent) laneRect = stickParent.rect;
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
    
    // Estimate slope & curvature of the target path around the hit moment.
void GetTargetDerivatives(int nowH, RhythmTypes.KnobSpan s, out float slopePerBeat, out float curvaturePerBeat2)
{
    // sample ~quarter beat on each side
    int dS = Mathf.Max(1, Mathf.RoundToInt(conductor.SamplesPerBeat * 0.25f));
    float tPrev = Mathf.Clamp01(s.targetAtSample(nowH - dS));
    float tNow  = Mathf.Clamp01(s.targetAtSample(nowH));
    float tNext = Mathf.Clamp01(s.targetAtSample(nowH + dS));

    // central difference for slope; scale to "per beat"
    float dtBeats = dS / Mathf.Max(1f, (float)conductor.SamplesPerBeat);
    slopePerBeat = (tNext - tPrev) / Mathf.Max(1e-4f, (2f * dtBeats));

    // discrete 2nd derivative (curvature proxy), normalized to per-beat^2
    curvaturePerBeat2 = (tNext - 2f * tNow + tPrev) / Mathf.Max(1e-4f, (dtBeats * dtBeats));
}

// Apply SDVX-like magnet with asymmetric approach/leave and corner boost.
// Returns the possibly-adjusted player01.
float ApplyMagnetAssist(float player01, int nowH, RhythmTypes.KnobSpan s, bool engaged)
{
    if (!magnetEnabled) return player01;
    if (magnetOnlyWhenEngaged && !engaged) return player01;

    // error on hit timeline
    float target01 = Mathf.Clamp01(s.targetAtSample(nowH));
    float err = target01 - player01;
    float absErr = Mathf.Abs(err);

    // approach vs leave (is error shrinking?)
    bool approaching = Mathf.Abs(err) < Mathf.Abs(_prevErr);

    // choose base gain/radius
    float baseGain   = magnetAsymmetric ? (approaching ? approachGain : leaveGain) : magnetGain;
    float baseRadius = magnetAsymmetric ? (approaching ? approachRadius : leaveRadius) : magnetRadius;

    // optional hysteresis snap when super close
    float extraSnap = (magnetHysteresis && absErr <= innerSnapRadius) ? innerSnapGain : 0f;

    // corner detection & boost
    float gainMul = 1f, radiusMul = 1f;
    if (cornerBoost)
    {
        GetTargetDerivatives(nowH, s, out float slopeB, out float curvB2);
        if (Mathf.Abs(slopeB) >= slopePerBeatThresh || Mathf.Abs(curvB2) >= curvatureThresh)
        {
            gainMul   = cornerGainMul;
            radiusMul = cornerRadiusMul;
        }
    }

    float useGain   = Mathf.Clamp01(baseGain * gainMul + extraSnap);
    float useRadius = Mathf.Clamp(baseRadius * radiusMul, 0f, 0.25f);

    // only help when reasonably close
    if (absErr > useRadius) { _prevErr = err; return player01; }

    // tempo-aware: per beat cap, then scaled by beats this frame
    float beatsThisFrame = Time.unscaledDeltaTime * (conductor.Bpm / 60f);
    float perBeat = Mathf.Clamp(err * useGain, -magnetMax01PerBeat, magnetMax01PerBeat);
    float assist  = perBeat * beatsThisFrame;

    float out01 = Mathf.Clamp01(player01 + assist);
    _prevErr = err;
    return out01;
}

}
