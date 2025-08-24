using UnityEngine;
using UnityEngine.InputSystem;

public class RhythmConductor : MonoBehaviour {
    [Header("Audio")]
    [SerializeField] AudioSource music;
    [SerializeField] float leadInSeconds = 2f;

    [Header("Timing Offsets (ms)")]
    [SerializeField] int inputOffsetMs = 0;
    [SerializeField] int visualOffsetMs = 0;

    [SerializeField] int bpm = 120;
    public int Bpm => bpm;

    public int SamplesPerBeat => Mathf.RoundToInt(SampleRate * 60f / Mathf.Max(1, bpm));
    public int SampleTime => NowForVisual;

    public int InputOffsetMs { get => inputOffsetMs; set => inputOffsetMs = value; }
    public int VisualOffsetMs { get => visualOffsetMs; set => visualOffsetMs = value; }

    int MsToSamples(int ms) => Mathf.RoundToInt(ms * 0.001f * SampleRate);
    public int NowForHit    => NowSample + MsToSamples(inputOffsetMs);
    public int NowForVisual => NowSample + MsToSamples(visualOffsetMs);
    public int SampleRate => music && music.clip ? music.clip.frequency : 48000;

    bool started;
    float leadClock;

    public bool Started => started;
    public AudioSource Music => music;

    public event System.Action SongFinished;
    int lastSampleAtFinish;
    bool finished;
    public bool IsFinished => finished;
    
    [SerializeField] int endPadSamples = 2048;
    bool suspended;                 // app focus / pause or editor mute
    bool wasSuspendedLastFrame;
    void OnApplicationFocus(bool focus) => suspended = !focus;
    void OnApplicationPause(bool pause) => suspended = pause;
    
    public void LockEnd()
    {
        if (music && music.clip) lastSampleAtFinish = music.clip.samples;
        finished = true;
        started = false;
        if (music) music.Stop();
    }
    public void ResetForIntro(float prerollSeconds)
    {
        finished = false;
        started = false;
        leadClock = 0f;
        leadInSeconds = Mathf.Max(0f, prerollSeconds);
        if (music) { music.Stop(); music.time = 0f; }
    }
    
    // RhythmConductor.cs  (add inside the class)
    public void SetBpm(int newBpm)
    {
        bpm = Mathf.Max(1, newBpm);
    }

    public void ArmAfter(float seconds)
    {
        // Stop anything that might be playing and re-arm the lead timer.
        if (music) music.Stop();
        started = false;
        leadClock = 0f;
        leadInSeconds = Mathf.Max(0f, seconds);
    }


    public int NowSample {
        get {
            // When finished, freeze time at the final sample so spawners don't restart.
            if (finished) return lastSampleAtFinish;

            // Before start, count down a negative pre-roll (visuals/judging use offsets).
            if (!started) return -Mathf.RoundToInt(Mathf.Max(0, leadInSeconds - leadClock) * SampleRate);

            // While playing, read from the AudioSource.
            return music ? music.timeSamples : 0;
        }
    }

   void Update()
{
    // Treat editor "Mute Audio" as suspension too
    bool editorMuted = AudioListener.pause;
    bool isSuspended = suspended || editorMuted;

    // While suspended: pause and do nothing else
    if (isSuspended)
    {
        if (started && music && music.isPlaying) music.Pause();
        wasSuspendedLastFrame = true;
        return;
    }

    // Just resumed: if we were playing before, unpause (but only if not at end)
    if (wasSuspendedLastFrame)
    {
        wasSuspendedLastFrame = false;
        if (started && music && music.clip)
        {
            int endSamples = Mathf.Max(0, music.clip.samples - endPadSamples);
            if (!music.isPlaying && music.timeSamples < endSamples)
                music.UnPause();
        }
    }

    // --- Pre-roll / lead-in (only if we haven't already finished) ---
    if (!started && !finished)
    {
        leadClock += Time.unscaledDeltaTime;

        if (leadClock >= leadInSeconds && music && music.clip)
        {
            if (!music.isPlaying)
            {
                music.time = 0f;
                music.loop = false;    // safety
                music.Play();
            }
            started = true;
        }
    }

    // --- Optional dev hotkeys ---
    if (Keyboard.current != null)
    {
        if (Keyboard.current.leftArrowKey.wasPressedThisFrame)  inputOffsetMs -= 5;
        if (Keyboard.current.rightArrowKey.wasPressedThisFrame) inputOffsetMs += 5;
    }

    // --- End-of-song detection & lock ---
    if (started && music && music.clip)
    {
        int endSamples = Mathf.Max(0, music.clip.samples - endPadSamples);

        // Finish only if we're *near the end* and playback stopped
        if (!music.isPlaying && music.timeSamples >= endSamples)
        {
            lastSampleAtFinish = music.clip.samples;   // freeze at exact end
            started  = false;
            finished = true;
            SongFinished?.Invoke();
            return;
        }

        // Safety: if loop was left on and we wrapped, end once
        if (music.loop && music.timeSamples < endPadSamples)
        {
            lastSampleAtFinish = music.clip.samples;
            music.loop = false;
            music.Stop();
            started  = false;
            finished = true;
            SongFinished?.Invoke();
            return;
        }
    }
}


}
