// Assets/Scripts/ScoreOdometer.cs
using UnityEngine;
using TMPro;

public class ScoreOdometer : MonoBehaviour
{
    [SerializeField] ScoreTracker tracker;   // optional; auto-finds
    [SerializeField] TMP_Text label;         // optional; auto-finds

    [Header("Display")]
    [SerializeField] int digits = 8;         // 00000000

    [Header("Adaptive rolling")]
    [Tooltip("How quickly the display closes the gap to the real score (seconds to catch up). Smaller = faster.")]
    [SerializeField] float catchUpSeconds = 0.25f;

    [Tooltip("Extra baseline speed, so it never feels stuck (points/sec).")]
    [SerializeField] float minExtraPerSecond = 15000f;

    [Tooltip("If backlog exceeds this, snap a chunk instantly to avoid 'infinite roll'.")]
    [SerializeField] int snapThreshold = 300_000;

    int displayed;
    int target;
    int prevTarget;           // last frame's target (to estimate incoming rate)
    float lastT;              // last unscaled time

    void Awake()
    {
        if (!label)   label   = GetComponent<TMP_Text>() ?? GetComponentInChildren<TMP_Text>(true);
        if (!tracker) tracker = FindFirstObjectByType<ScoreTracker>() ?? FindObjectOfType<ScoreTracker>();

        displayed = target = prevTarget = tracker ? tracker.Score : 0;
        lastT = Time.unscaledTime;

        if (label)
        {
            label.enabled = true;
            label.alpha = 1f;
            label.text = Format(displayed);
        }
    }

    void OnEnable()
    {
        if (!tracker) tracker = FindFirstObjectByType<ScoreTracker>() ?? FindObjectOfType<ScoreTracker>();
        if (tracker)
        {
            tracker.OnScoreChanged -= HandleScoreChanged; // safety
            tracker.OnScoreChanged += HandleScoreChanged;
            // sync immediately
            HandleScoreChanged(tracker.Score);
        }
    }

    void OnDisable()
    {
        if (tracker) tracker.OnScoreChanged -= HandleScoreChanged;
    }

    void HandleScoreChanged(int newScore)
    {
        target = Mathf.Max(0, newScore);
        // we don't start a coroutine; Update() adapts every frame
    }

    void Update()
    {
        if (!label) return;

        float now = Time.unscaledTime;
        float dt  = Mathf.Max(1e-4f, now - lastT);
        lastT = now;

        // backlog to consume
        int backlog = target - displayed;

        // keep showing zero-padded even when equal
        if (backlog <= 0)
        {
            if (label) label.text = Format(displayed);
            prevTarget = target;
            return;
        }

        // LARGE backlog → snap a chunk right away so we visibly catch up
        if (backlog >= snapThreshold)
        {
            int snap = Mathf.Min(backlog, snapThreshold / 2); // snap half the threshold
            displayed += snap;
            label.text = Format(displayed);
            prevTarget = target;
            return;
        }

        // Estimate how fast the real score is moving (points/sec)
        int   targetDelta     = target - prevTarget;
        float incomingPerSec  = targetDelta / dt;         // can be 0..big during trace
        if (incomingPerSec < 0f) incomingPerSec = 0f;     // guard against rare negative

        // We want to keep up with incoming AND eat the backlog within catchUpSeconds
        float catchupPerSec   = backlog / Mathf.Max(0.05f, catchUpSeconds);
        float desiredPerSec   = incomingPerSec + catchupPerSec + minExtraPerSecond;

        // Advance this frame
        int step = Mathf.CeilToInt(desiredPerSec * dt);
        step = Mathf.Clamp(step, 1, backlog);

        displayed += step;
        label.text = Format(displayed);

        prevTarget = target;
    }

    string Format(int v) => v.ToString($"D{digits}");
}
