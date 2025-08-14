using UnityEngine;
using System.Collections.Generic;

public class RhythmTestBootstrap : MonoBehaviour
{
    [SerializeField] RhythmConductor conductor;
    [SerializeField] ButtonLaneController buttonLane;
    [SerializeField] KnobLaneController   knobLane;

    void Start()
    {
        int sr = conductor.SampleRate;

        var notes = new List<RhythmTypes.ButtonNote> {
            new(){ startSample = sr*2, endSample = sr*2, button = RhythmTypes.FaceButton.A },
            new(){ startSample = sr*3, endSample = sr*3, button = RhythmTypes.FaceButton.Y },
            new(){ startSample = sr*4, endSample = sr*4, button = RhythmTypes.FaceButton.B },
            new(){ startSample = sr*5, endSample = sr*5, button = RhythmTypes.FaceButton.X },
        };
        buttonLane.LoadChart(notes);

        var spans = new List<RhythmTypes.KnobSpan> {
            new RhythmTypes.KnobSpan {
                startSample = sr*2, endSample = sr*4,
                targetAtSample = (s)=>{
                    float t = Mathf.InverseLerp(sr*2, sr*4, s); // 0..1
                    return Mathf.SmoothStep(0.2f, 0.8f, t);     // simple slope
                }
            }
        };
        knobLane.LoadChart(spans);
    }
}