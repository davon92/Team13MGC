using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Koreographer runtime API
using SonicBloom.Koreo;

public class KoreographyChartLoader : MonoBehaviour
{
    [Header("Koreography source")]
    [SerializeField] Koreography koreography;
    [SerializeField] KnobPathRenderer knobPath;

    [Tooltip("Event ID for the face-button track (Int payload: 0=A,1=Y,2=B,3=X).")]
    [SerializeField] string buttonsEventID = "BTN";

    [Tooltip("Event ID for the knob track (Curve payload on Span, 0..1).")]
    [SerializeField] string knobEventID = "KNOB";

    [Header("Targets")]
    [SerializeField] ButtonLaneController buttonLane;
    [SerializeField] KnobLaneController   knobLane;
    [SerializeField] RhythmConductor      conductor;

    [Header("Optional")]
    [Tooltip("If true, log what was parsed so you can verify the chart in the Console.")]
    [SerializeField] bool verbose = false;
    
    public void SetKoreography(SonicBloom.Koreo.Koreography k) { koreography = k; _dirty = true; }
    public void SetButtonsEventID(string id) { buttonsEventID = id; _dirty = true; }
    public void SetKnobEventID(string id)    { knobEventID    = id; _dirty = true; }
    public void SetKnobPath(KnobPathRenderer path) { knobPath = path; _dirty = true; }
    
    bool _loaded, _dirty;
    void OnEnable() { _loaded = false; }
    
    void Start() { TryLoad(); }
    void Update() { if (!_loaded && _dirty) TryLoad(); }

    void TryLoad()
    {
        if (_loaded) return;
        if (!koreography) return;   // not set yet by RhythmEntryPoint

        var buttons = ParseButtons();
        if (buttonLane) buttonLane.LoadChart(buttons);

        var knobSpans = ParseKnob();
        if (knobLane) knobLane.LoadChart(knobSpans);
        if (knobPath) knobPath.SetSpans(knobSpans);

        _loaded = true;
        _dirty = false;

        if (verbose) Debug.Log($"[KoreographyChartLoader] Loaded '{koreography.name}' " +
                               $"buttons={buttons.Count} knobSpans={knobSpans.Count}");
    }

    // --- Parse the Buttons track (Int payload 0..3; OneOff=Tap, Span=Hold)
    List<RhythmTypes.ButtonNote> ParseButtons()
    {
        var notes = new List<RhythmTypes.ButtonNote>();

        KoreographyTrackBase track = koreography.GetTrackByID(buttonsEventID);
        if (track == null)
        {
            Debug.LogWarning($"[KoreographyChartLoader] Buttons track '{buttonsEventID}' not found.");
            return notes;
        }

        var evts = track.GetAllEvents();              // list of KoreographyEvent
        foreach (var evt in evts)
        {
            // Expect Int payload; fallback: A if absent.
            int lane = 0;
            if (evt.HasIntPayload()) lane = Mathf.Clamp(evt.GetIntValue(), 0, 3);

            var face = (RhythmTypes.FaceButton)lane;

            var n = new RhythmTypes.ButtonNote
            {
                startSample = evt.StartSample,
                endSample   = evt.EndSample,   // == start for taps; > start for holds
                button      = face,
                chordId     = -1               // extend later if you add chords
            };

            notes.Add(n);

            if (verbose)
            {
                bool isHold = evt.EndSample > evt.StartSample;
                Debug.Log($"[KoreographyChartLoader] BTN {face} @ {evt.StartSample} {(isHold ? $"→ {evt.EndSample}" : "(tap)")}");
            }
        }

        return notes.OrderBy(n => n.startSample).ToList();
    }

    // --- Parse the Knob track (Curve payload on Span). Generates sampling funcs.
    List<RhythmTypes.KnobSpan> ParseKnob()
    {
        var spans = new List<RhythmTypes.KnobSpan>();

        KoreographyTrackBase track = koreography.GetTrackByID(knobEventID);
        if (track == null)
        {
            Debug.LogWarning($"[KoreographyChartLoader] Knob track '{knobEventID}' not found.");
            return spans;
        }

        var evts = track.GetAllEvents();              // list of KoreographyEvent
        foreach (var evt in evts)
        {
            if (evt.EndSample <= evt.StartSample)     // must be a Span
                continue;

            // Prefer Curve payload on the Span; fallback to Float (constant).
            bool   hasCurve = evt.HasCurvePayload();
            bool   hasFloat = !hasCurve && evt.HasFloatPayload();
            string desc     = hasCurve ? "curve" : hasFloat ? "const" : "none";

            // Build a sampler that returns 0..1 target at any sample within the span.
            System.Func<int, float> sampler = (sample) =>
            {
                float v = 0.5f; // default middle if no payload
                if (hasCurve)
                {
                    // Built-in accessor samples the curve by time (sample index).
                    // Returns float (use Clamp01 so the lane is always in range).
                    v = Mathf.Clamp01(evt.GetValueOfCurveAtTime(sample));
                }
                else if (hasFloat)
                {
                    v = Mathf.Clamp01(evt.GetFloatValue());
                }
                else
                {
                    // If no payload, linearly map 0->1 across the span.
                    float t = Mathf.InverseLerp(evt.StartSample, evt.EndSample, sample);
                    v = Mathf.Clamp01(t);
                }
                return v;
            };

            spans.Add(new RhythmTypes.KnobSpan
            {
                startSample    = evt.StartSample,
                endSample      = evt.EndSample,
                targetAtSample = sampler
            });

            if (verbose)
            {
                Debug.Log($"[KoreographyChartLoader] KNOB {desc} span {evt.StartSample} → {evt.EndSample}");
            }
        }

        return spans.OrderBy(s => s.startSample).ToList();
    }

}
