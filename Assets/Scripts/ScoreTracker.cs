// Assets/Scripts/ScoreTracker.cs
using System;
using UnityEngine;

public class ScoreTracker : MonoBehaviour
{
    [Header("Scoring (raw, pre-normalization)")]
    public int MaxScore   = 10_000_000;
    public int TapPerfect = 10000;
    public int TapGreat   = 7000;
    public int TapGood    = 4000;
    public int TracePerBeat = 6000;            // per beat @100% quality (pre-norm)

    [Header("Trace → scoring & combo")]
    [Range(0f, 1f)] public float TraceQualityFloor = 0f; // <- was 0.40f
    [Tooltip("Add +1 combo every N beats while tracing on target.")]
    public float TraceComboEveryBeats = 1.0f;  // ← slower: once per beat

    // ----- Runtime -----
    double _norm = 1.0;
    double _scoreAccum;
    public  int Score { get; private set; }

    public  int Combo    { get; private set; }
    public  int MaxCombo { get; private set; }

    float  _traceComboBank;
    bool   _traceComboLocked = false;  // locked after Miss until next Good+
    bool _normalizedReady = false;
    ButtonLaneController _buttons;
    KnobLaneController   _knob;
    RhythmConductor      _conductor;
    
    // --- DEBUG: last frame scoring info ---
    public int   DebugLastDelta { get; private set; }   // how many points were actually added last change
    public string DebugLastTag  { get; private set; }   // where it came from ("Tap Perfect", "Trace tick 1.00*0.016", etc.)
    public double DebugNorm     => _norm;               // current normalization factor
    public bool   DebugNormReady => _normalizedReady;   // true once ConfigureNormalization succeeded

    public event Action<int> OnScoreChanged;
    public event Action<int> OnComboChanged;

    // ---------- Lifecycle hooks ----------
    public void Hook(ButtonLaneController buttons, KnobLaneController knob, RhythmConductor conductor)
    {
        Unhook();

        _buttons   = buttons;
        _knob      = knob;
        _conductor = conductor;

        if (_buttons) _buttons.OnJudged += HandleJudgement;
        if (_knob)    _knob.OnJudged    += HandleJudgement;

        // Reset
        _scoreAccum = 0;
        Score       = 0;
        Combo       = 0;
        MaxCombo    = 0;
        _traceComboBank   = 0f;
        _traceComboLocked = false;

        RaiseScoreChanged();
        RaiseComboChanged();
        // NOTE: Do NOT call ConfigureNormalization here; call it AFTER both lanes load.
    }

    public void Unhook()
    {
        if (_buttons) { _buttons.OnJudged -= HandleJudgement; _buttons = null; }
        if (_knob)    { _knob.OnJudged    -= HandleJudgement; _knob    = null; }
        _conductor = null;
    }

    /// <summary>Call AFTER both lanes have loaded their charts.</summary>
    public void ConfigureNormalization(int totalTapNotes, double totalTraceBeats)
    {
        double perfectSum = (double)TapPerfect * totalTapNotes
                            + (double)TracePerBeat * totalTraceBeats;

        if (perfectSum <= 0.0)
        {
            _norm = 1.0;
            _normalizedReady = false;  // do NOT award until we get real totals
#if UNITY_EDITOR
            Debug.LogWarning("[ScoreTracker] Normalization skipped (no totals yet).");
#endif
            return;
        }

        _norm = (double)MaxScore / perfectSum;
        _normalizedReady = true;

#if UNITY_EDITOR
        Debug.Log($"[ScoreTracker] Normalization set. taps={totalTapNotes}, beats={totalTraceBeats:0.00}, norm={_norm:0.000000}");
#endif
    }


    // ---------- External scoring hooks ----------
    public void KnobTraceTick(float quality01, float beatsThisFrame)
    {
        if (!_normalizedReady) return;
        if (beatsThisFrame <= 0f || quality01 <= 0f) return;

        DebugLastTag = $"Trace tick {quality01:0.00}*{beatsThisFrame:0.000}";

        var q = System.Math.Clamp((double)Mathf.Max(TraceQualityFloor, quality01), 0.0, 1.0);
        double raw = TracePerBeat * q * beatsThisFrame;
        AddScoreRaw((int)System.Math.Round(raw));

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


    public SceneFlow.RhythmResult BuildResult()
    {
        var r = new SceneFlow.RhythmResult();
        r.score    = Score;
        r.maxCombo = MaxCombo;
        return r;
    }

    // ---------- Internals ----------
    void HandleJudgement(Judgement j)
    {
        switch (j)
        {
            case Judgement.Perfect: DebugLastTag = "Tap Perfect"; AddScoreRaw(TapPerfect); AddCombo(1); _traceComboLocked = false; break;
            case Judgement.Great:   DebugLastTag = "Tap Great";   AddScoreRaw(TapGreat);   AddCombo(1); _traceComboLocked = false; break;
            case Judgement.Good:    DebugLastTag = "Tap Good";    AddScoreRaw(TapGood);    AddCombo(1); _traceComboLocked = false; break;
            case Judgement.Miss:    DebugLastTag = "Tap Miss";    BreakCombo();            _traceComboLocked = true; _traceComboBank = 0f; break;
        }
    }

    void AddScoreRaw(int raw)
    {
        if (raw <= 0) return;

        int before = Score;

        _scoreAccum += raw * _norm;
        int clamped = (int)System.Math.Min(_scoreAccum, MaxScore);
        int delta   = clamped - before;

        if (delta != 0)
        {
            Score = clamped;
            DebugLastDelta = delta;           // <-- store last change
            OnScoreChanged?.Invoke(Score);
        }
    }


    void AddCombo(int delta)
    {
        Combo += delta;
        if (Combo > MaxCombo) MaxCombo = Combo;
        RaiseComboChanged();
    }

    void BreakCombo()
    {
        if (Combo == 0) return;
        Combo = 0;
        RaiseComboChanged();
    }

    void RaiseScoreChanged() => OnScoreChanged?.Invoke(Score);
    void RaiseComboChanged() => OnComboChanged?.Invoke(Combo);
}
