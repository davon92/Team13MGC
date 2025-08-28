// Assets/Scripts/ScoreTracker.cs
using System;
using UnityEngine;

public class ScoreTracker : MonoBehaviour
{
    [Header("Target total")]
    public int MaxScore = 10_000_000;

    [Header("Tap raw units (before normalization)")]
    public int TapPerfectRaw = 1000;
    public int TapGreatRaw   = 700;
    public int TapGoodRaw    = 400;

    [Header("Trace raw units (before normalization)")]
    public int TracePerBeatRaw = 1000;   // full-credit per beat while tracing
    [Range(0f, 1f)] public float TraceQualityFloor = 0f;   // 0 → no free points out of window

    [Header("Trace → combo growth")]
    public float TraceComboEveryBeats = 1.0f;  // add +1 combo per this many traced beats

    // Exposed for HUD/odometer
    public int Score { get; private set; }
    public int Combo { get; private set; }

    // Events
    public event Action<int> OnScoreChanged;
    public event Action<int> OnComboChanged;

    // Debug hooks (optional)
    public int    DebugLastDelta { get; private set; }
    public string DebugLastTag   { get; private set; }
    public double DebugNorm      => _norm;
    public bool   DebugNormReady => _normalizedReady;

    // Internals
    ButtonLaneController _buttons;
    KnobLaneController   _knob;
    RhythmConductor      _conductor;

    double _accumUnits;          // raw “units” accumulated (pre-normalization)
    double _norm = 0.0;          // units → points scale so perfect play reaches MaxScore
    bool   _normalizedReady = false;

    // trace combo throttling
    float _traceComboBank;
    bool  _traceComboLocked;     // set after Miss; unlocks on next Good+

    // ---------- Wiring ----------
    public void Hook(ButtonLaneController buttons, KnobLaneController knob, RhythmConductor conductor)
    {
        // unhook old
        if (_buttons) _buttons.OnJudged -= HandleJudgement;
        if (_knob)    _knob.OnJudged    -= HandleJudgement;

        _buttons   = buttons;
        _knob      = knob;
        _conductor = conductor;

        if (_buttons) _buttons.OnJudged += HandleJudgement;
        if (_knob)    _knob.OnJudged    += HandleJudgement;

        // reset runtime state
        _accumUnits       = 0.0;
        _traceComboBank   = 0f;
        _traceComboLocked = false;
        Combo             = 0;
        Score             = 0;
        OnScoreChanged?.Invoke(Score);   // makes odometer show 00000000 immediately
        OnComboChanged?.Invoke(0);
    }

    /// Call AFTER lanes are loaded and their totals are non-zero.
    public void ConfigureNormalization(int totalTapNotes, double totalTraceBeats)
    {
        // Include knob head judgements (one per span) with taps:
        int knobHeads = (_knob != null) ? Mathf.Max(0, _knob.TotalTraceHeads) : 0;
        int totalTapLike = Mathf.Max(0, totalTapNotes) + knobHeads;

        // Units a PERFECT run would accumulate:
        //   - every tap/head at TapPerfectRaw
        //   - every trace beat at TracePerBeatRaw
        double perfectUnits = (double)TapPerfectRaw * (double)totalTapLike
                            + (double)TracePerBeatRaw * Mathf.Max(0f, (float)totalTraceBeats);

        if (perfectUnits <= 0.0)
        {
#if UNITY_EDITOR
            Debug.LogWarning("[ScoreTracker] Normalization skipped (no totals). Waiting…");
#endif
            _norm = 0.0;
            _normalizedReady = false;
            return;
        }

        _norm = (double)MaxScore / perfectUnits;
        _normalizedReady = true;

        // snap current score to normalized value
        RecomputeScoreFromAccum();

#if UNITY_EDITOR
        Debug.Log($"[ScoreTracker] Norm ready. taps={totalTapNotes}, heads={knobHeads}, traceBeats={totalTraceBeats:0.00}, norm={_norm:0.000000}");
#endif
    }

    // ---------- Judgements from lanes ----------
    void HandleJudgement(Judgement j)
    {
        switch (j)
        {
            case Judgement.Perfect:
                DebugLastTag = "Tap Perfect";
                AddUnits(TapPerfectRaw);
                AddCombo(1);
                _traceComboLocked = false;
                break;

            case Judgement.Great:
                DebugLastTag = "Tap Great";
                AddUnits(TapGreatRaw);
                AddCombo(1);
                _traceComboLocked = false;
                break;

            case Judgement.Good:
                DebugLastTag = "Tap Good";
                AddUnits(TapGoodRaw);
                AddCombo(1);
                _traceComboLocked = false;
                break;

            case Judgement.Miss:
                DebugLastTag = "Tap Miss";
                BreakCombo();
                _traceComboLocked = true;   // tracing won’t grow combo until next Good+
                _traceComboBank   = 0f;
                break;
        }
    }

    /// Continuous scoring from knob tracing (call only when tick>0 and beats>0).
    public void KnobTraceTick(float quality01, float beatsThisFrame)
    {
        if (beatsThisFrame <= 0f || quality01 <= 0f) return;

        // until normalized, keep accumulating raw units; UI will catch up after ConfigureNormalization
        double q = Math.Clamp((double)Mathf.Max(TraceQualityFloor, quality01), 0.0, 1.0);
        double rawUnits = (double)TracePerBeatRaw * q * (double)beatsThisFrame;

        DebugLastTag = $"Trace {q:0.00}×{beatsThisFrame:0.000} beats";
        AddUnits(rawUnits);

        // trace also grows combo over time (unless we’re “locked” by a recent Miss)
        if (!_traceComboLocked && quality01 > 0f)
        {
            _traceComboBank += beatsThisFrame;
            while (_traceComboBank >= TraceComboEveryBeats)
            {
                AddCombo(1);
                _traceComboBank -= TraceComboEveryBeats;
            }
        }
    }

    // ---------- Core accumulation / score publish ----------
    void AddUnits(double units)
    {
        if (units <= 0.0) return;

        _accumUnits += units;

        if (!_normalizedReady) return; // don’t publish points until norm is ready

        int before = Score;
        int after  = ClampToPoints(_accumUnits);
        if (after != before)
        {
            Score = after;
            DebugLastDelta = Score - before;
            OnScoreChanged?.Invoke(Score);
        }
    }

    void RecomputeScoreFromAccum()
    {
        int s = ClampToPoints(_accumUnits);
        if (s != Score)
        {
            int before = Score;
            Score = s;
            DebugLastDelta = Score - before;
            OnScoreChanged?.Invoke(Score);
        }
    }

    int ClampToPoints(double units)
    {
        if (!_normalizedReady) return 0;
        double pts = units * _norm;
        if (pts < 0.0) return 0;
        long val = (long)Math.Floor(pts + 1e-9);
        if (val > MaxScore) val = MaxScore;
        return (int)val;
    }

    // ---------- Combo helpers ----------
    void AddCombo(int inc)
    {
        Combo = Mathf.Max(0, Combo + inc);
        OnComboChanged?.Invoke(Combo);
    }

    void BreakCombo()
    {
        if (Combo != 0)
        {
            Combo = 0;
            OnComboChanged?.Invoke(0);
        }
    }

    // Optional: used by RhythmEntryPoint
    public SceneFlow.RhythmResult BuildResult()
    {
        var rr = new SceneFlow.RhythmResult();
        rr.score = Score;
        // Fill other fields upstream as you already do in RhythmEntryPoint.
        return rr;
    }
}
