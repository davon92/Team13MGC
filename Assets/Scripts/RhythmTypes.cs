// RhythmTypes.cs
using System;
using System.Collections.Generic;

public static class RhythmTypes
{
    public enum FaceButton { A = 0, B = 1, X = 2, Y = 3 }

    [Serializable]
    public struct ButtonNote
    {
        public int startSample;     // center crosses hit line here
        public int endSample;       // == startSample for tap; > startSample for hold
        public FaceButton button;
        public int chordId;         // optional; -1 if not part of a chord
    }

    // Runtime form for knob segments
    public struct KnobSpan
    {
        public int startSample, endSample;
        public Func<int, float> targetAtSample; // 0..1 at a given sample
    }
}

[Serializable]
public class RhythmChart
{
    public List<RhythmTypes.ButtonNote> buttonNotes = new();
    public List<RhythmTypes.KnobSpan>   knobSpans   = new();
}