// RhythmEntryPoint.cs
using UnityEngine;
using System.Collections;
using TMPro;

public class RhythmEntryPoint : MonoBehaviour
{
    [Header("Scene Wiring")]
    [SerializeField] KoreographyChartLoader chartLoader;
    [SerializeField] RhythmConductor        conductor;
    public ButtonLaneController buttons;
    public KnobLaneController   knob;
    [SerializeField] AudioSource            musicSource;

    [Header("Intro Sequence")]
    [SerializeField] Animator curtainAnimator;
    [SerializeField] string   curtainTrigger = "Open";
    [SerializeField] float    curtainSeconds = 3f;
    [SerializeField] float    prerollSeconds = 2f;

    [Header("Outro")]
    [SerializeField] float outroHoldSeconds = 3f;     // <- 3s breathe out
    [SerializeField] float fadeOutSeconds   = 0.6f;   // <- fade length
    [SerializeField] ScoreTracker score;

    bool ending;   // prevent double-outro

    void Awake()
    {
        var req = SceneFlow.PendingRhythm;
        if (req == null || req.song == null) return;

        var song = req.song;

        if (!musicSource) musicSource = FindFirstObjectByType<AudioSource>();
        if (!conductor)   conductor   = FindFirstObjectByType<RhythmConductor>();
        if (!buttons)   buttons   = FindFirstObjectByType<ButtonLaneController>();
        if (!knob)      knob      = FindFirstObjectByType<KnobLaneController>();
        if (!chartLoader) chartLoader = FindFirstObjectByType<KoreographyChartLoader>();

        if (musicSource && song.Music)
        {
            musicSource.clip = song.Music;
            musicSource.playOnAwake = false;
            musicSource.loop = false;        // IMPORTANT: don’t loop
        }

        if (chartLoader && song.koreography)
        {
            chartLoader.SendMessage("SetKoreography", song.koreography, SendMessageOptions.DontRequireReceiver);
            chartLoader.SendMessage("SetButtonsEventID", song.buttonsEventID, SendMessageOptions.DontRequireReceiver);
            chartLoader.SendMessage("SetKnobEventID",    song.knobEventID,    SendMessageOptions.DontRequireReceiver);
        }

        if (conductor) conductor.SetBpm(Mathf.RoundToInt(song.bpm));
        foreach (var grid in FindObjectsByType<BeatGridScroller>(FindObjectsSortMode.None))
            grid.SetBpm(song.bpm);
        
        if (musicSource)
        {
            musicSource.playOnAwake = false;
            musicSource.loop = false;        // <- make absolutely sure
        }
    }
    
    

    void Start() => StartCoroutine(CoIntro());

    IEnumerator CoIntro()
    {
        if (conductor) conductor.enabled = false;

        if (curtainAnimator && !string.IsNullOrEmpty(curtainTrigger))
            curtainAnimator.SetTrigger(curtainTrigger);

        if (curtainSeconds > 0f)
            yield return new WaitForSecondsRealtime(curtainSeconds);

        if (conductor)
        {
            conductor.enabled = true;
            conductor.ArmAfter(prerollSeconds);
        }
    }

    void OnEnable()
    {
        if (!conductor) conductor = FindFirstObjectByType<RhythmConductor>();
        if (conductor) conductor.SongFinished += HandleSongFinished;

        if (!score) score = FindFirstObjectByType<ScoreTracker>();

        // Hook scoring to lanes
        if (score && buttons && knob && conductor)
            score.Hook(buttons, knob, conductor);

        // Normalize AFTER charts are loaded into the lanes
        StartCoroutine(CoNormalizeAfterLoad());
    }
    
    void OnDisable()
    {
        if (conductor) conductor.SongFinished -= HandleSongFinished;
    }

    void HandleSongFinished()
    {
        if (ending) return;
        ending = true;

        if (!score) score = FindFirstObjectByType<ScoreTracker>();

        var result = score ? score.BuildResult() : new SceneFlow.RhythmResult();

        var req = SceneFlow.PendingRhythm;
        result.song   = req?.song;
        result.origin = req?.origin ?? SceneFlow.RhythmOrigin.SongSelect;
        result.cleared = true;

        SceneFlow.SubmitRhythmResult(result);

        conductor?.LockEnd();
        musicSource?.Stop();
        var path = FindFirstObjectByType<KnobPathRenderer>();
        if (path) path.enabled = false;

        StartCoroutine(CoOutro());
    }
    
    IEnumerator CoOutro()
    {
        foreach (var b in FindObjectsByType<ButtonLaneController>(FindObjectsSortMode.None))
        { b.enabled = false; b.ClearAll(); }
        foreach (var k in FindObjectsByType<KnobLaneController>(FindObjectsSortMode.None))
        { k.enabled = false; k.ClearAll(); }
        if (conductor) conductor.enabled = false;

        yield return new WaitForSecondsRealtime(outroHoldSeconds);

        if (Fade.Instance != null)
            yield return Fade.Instance.Out(fadeOutSeconds);

        // Use the result we already submitted
        _ = SceneFlow.ReturnFromRhythmAsync(SceneFlow.LastRhythmResult, alreadyFaded: true);
    }
    
    IEnumerator CoNormalizeAfterLoad()
    {
        // give the chart loader one frame to push data into the lanes
        yield return null;

        // wait until the lanes report any non-zero totals (or timeout)
        float timeout = 3f;
        while (timeout > 0f &&
               score && buttons && knob &&
               buttons.TotalScorableTaps == 0 &&
               knob.TotalTraceBeats == 0)
        {
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        if (score && buttons && knob)
            score.ConfigureNormalization(buttons.TotalScorableTaps, knob.TotalTraceBeats);
    }


    static IEnumerator FadeCanvas(CanvasGroup cg, float from, float to, float seconds)
    {
        cg.blocksRaycasts = true;
        cg.interactable = false;
        float t = 0f; cg.alpha = from;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(from, to, t / Mathf.Max(0.0001f, seconds));
            yield return null;
        }
        cg.alpha = to;
    }
    
    void ConfigureScoringForSong()
    {
        var taps  = buttons.TotalScorableTaps;
        var beats = knob.TotalTraceBeats;
        score.ConfigureNormalization(taps, beats);
    }
}
