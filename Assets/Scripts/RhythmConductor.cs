using UnityEngine;
using UnityEngine.InputSystem;

public class RhythmConductor : MonoBehaviour {
    [Header("Audio")]
    [SerializeField] AudioSource music;        // Koreographer’s player’s AudioSource
    [SerializeField] float leadInSeconds = 2f; // pre-roll visual time
    
    [Header("Timing Offsets (ms)")]
    [SerializeField] int inputOffsetMs = 0;   // compensate controller/audio latency
    [SerializeField] int visualOffsetMs = 0;  // nudge visuals if they feel ahead/behind

    public int InputOffsetMs { get => inputOffsetMs; set => inputOffsetMs = value; }
    public int VisualOffsetMs { get => visualOffsetMs; set => visualOffsetMs = value; }

    int MsToSamples(int ms) => Mathf.RoundToInt(ms * 0.001f * SampleRate);

// Use these instead of NowSample in gameplay code:
    public int NowForHit    => NowSample + MsToSamples(inputOffsetMs);
    public int NowForVisual => NowSample + MsToSamples(visualOffsetMs);
    public int SampleRate => music.clip.frequency;

    bool started;
    float leadClock;

    // negative during lead-in, then AudioSource timeSamples
    public int NowSample {
        get {
            if (!started) return -Mathf.RoundToInt(Mathf.Max(0, leadInSeconds - leadClock) * SampleRate);
            return music.timeSamples; // (your Koreographer “Event Delay” handles latency)
        }
    }

    void Update() {
        if (started) return;
        leadClock += Time.unscaledDeltaTime;
        if (leadClock >= leadInSeconds) { music.Play(); started = true; }
        
        if (Keyboard.current.leftArrowKey.wasPressedThisFrame)  inputOffsetMs -= 5;
        if (Keyboard.current.rightArrowKey.wasPressedThisFrame) inputOffsetMs += 5;
    }
}