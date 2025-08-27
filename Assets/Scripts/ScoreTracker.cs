using UnityEngine;
using System;

public class ScoreTracker : MonoBehaviour
{
    // --- Wiring (assign in inspector or via Hook(..)) ---
    [SerializeField] ButtonLaneController buttons;
    [SerializeField] KnobLaneController   knob;
    [SerializeField] RhythmConductor      conductor;

    // --- Public readouts (so UI can read live if you want) ---
    public int Perfect  { get; private set; }
    public int Great    { get; private set; }
    public int Good     { get; private set; }
    public int Miss     { get; private set; }

    public int Combo    { get; private set; }
    public int MaxCombo { get; private set; }

    public int Score    { get; private set; }   // 0..1,000,000
    public string Grade { get; private set; }   // e.g., S, AAA, AA…
    
    public event Action<int,int> OnComboChanged;
    // Weights match what you used for knob tick scoring.
    const float W_Perfect = 1.00f;
    const float W_Great   = 0.70f;
    const float W_Good    = 0.40f;
    const float W_Miss    = 0.00f;

    const int MAX_SCORE = 10_000_000;

    // We accumulate “units” so the final score can be 1e6 * (achieved / max).
    double achievedUnits, maxUnits;

    // Optional: while tracing the knob, add combo pulses (like SDVX).
    // We throttle combo bumps so it doesn’t explode every frame.
    [SerializeField] float comboPulseHzWhileTracing = 8f; // 8 bumps per second while on-target
    float comboPulseTimer;

    void Awake()
    {
        // You can also call Hook(..) from RhythmEntryPoint if you prefer code wiring.
        Subscribe();
        ResetForSong();
    }

    public void Hook(ButtonLaneController b, KnobLaneController k, RhythmConductor c)
    {
        buttons = b; knob = k; conductor = c;
        Subscribe();
    }

    void OnDisable()
    {
        if (buttons) buttons.OnJudged -= HandleJudgement;
        if (knob)
        {
            knob.OnJudged            -= HandleJudgement;
            knob.OnTraceSpanScored   -= HandleTraceSpanScored;
        }
    }

    void Subscribe()
    {
        if (buttons) buttons.OnJudged += HandleJudgement;      // button taps
        if (knob)
        {
            knob.OnJudged          += HandleJudgement;         // knob head (start) judgement
            knob.OnTraceSpanScored += HandleTraceSpanScored;   // continuous tracing quality
        }
    }

    public void ResetForSong()
    {
        Perfect = Great = Good = Miss = 0;
        Combo = MaxCombo = 0;
        Score = 0; Grade = "";
        achievedUnits = 0.0;
        maxUnits      = 0.0;
        comboPulseTimer = 0f;
        
        RaiseComboChanged(); // <- tell UI we're at 0
    }

    // ---------- Input from lanes ----------

    void HandleJudgement(Judgement j)
    {
        // counts …
        switch (j)
        {
            case Judgement.Perfect: Perfect++; break;
            case Judgement.Great:   Great++;   break;
            case Judgement.Good:    Good++;    break;
            default:                Miss++;    break;
        }

        // combo
        if (j == Judgement.Miss)
        {
            if (Combo != 0)
            {
                Combo = 0;
                RaiseComboChanged();
            }
        }
        else
        {
            BumpCombo();
        }

        // score units …
        achievedUnits += JudgementToWeight(j);
        maxUnits      += W_Perfect;
        RecomputeScore();
    }


    void HandleTraceSpanScored(float sum, float max)
    {
        // The knob lane already gave us “sum” as tick quality totals and “max”
        // as the best-possible ticks for the span — just add to the pool.
        achievedUnits += sum;
        maxUnits      += max;
        RecomputeScore();
    }

    // Optional: call this continuously while knob is inside the tracing window
    // with 'onTarget' true when tick > 0 (see tiny hook in KnobLaneController below).
    public void KnobTraceComboPulse(bool onTarget, float deltaTime)
    {
        if (!onTarget) return;

        if (comboPulseHzWhileTracing <= 0f) comboPulseHzWhileTracing = 8f;
        comboPulseTimer += deltaTime;
        float period = 1f / comboPulseHzWhileTracing;
        if (comboPulseTimer >= period)
        {
            comboPulseTimer -= period;
            BumpCombo();
        }
    }

    // ---------- Finish & export ----------

    void RecomputeScore()
    {
        double ratio = (maxUnits > 0.0) ? (achievedUnits / maxUnits) : 0.0;
        Score = Mathf.Clamp(Mathf.RoundToInt((float)(MAX_SCORE * ratio)), 0, MAX_SCORE);
        Grade = GradeFromScore(Score);
    }

    void BumpCombo()
    {
        Combo++;
        if (Combo > MaxCombo) MaxCombo = Combo;
        
        RaiseComboChanged();
    }

    float JudgementToWeight(Judgement j) =>
        j == Judgement.Perfect ? W_Perfect :
        j == Judgement.Great   ? W_Great   :
        j == Judgement.Good    ? W_Good    : W_Miss;

    string GradeFromScore(int s)
    {
        // Scale thresholds x10 to match 10,000,000 cap (tweak if you like)
        if (s >= 9_900_000) return "AAA";
        if (s >= 9_700_000) return "AA";
        if (s >= 9_400_000) return "A";
        if (s >= 9_000_000) return "B";
        if (s >= 8_500_000) return "C";
        return "D";
    }

    // Call this from RhythmEntryPoint when the song finishes (or subscribe to a SongFinished event)
    public SceneFlow.RhythmResult BuildResult()
    {
        return new SceneFlow.RhythmResult {
            score    = Score,
            grade    = Grade,
            maxCombo = MaxCombo,
            perfect  = Perfect,
            great    = Great,
            good     = Good,
            miss     = Miss
        };
    }
    
    void RaiseComboChanged()
    {
        OnComboChanged?.Invoke(Combo, MaxCombo);
    }
    
    public void BreakCombo()
    {
        if (Combo == 0) return;
        Combo = 0;
        RaiseComboChanged();
    }
}
