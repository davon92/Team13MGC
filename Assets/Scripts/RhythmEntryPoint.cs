// RhythmEntryPoint.cs
using UnityEngine;
using System.Collections;

public class RhythmEntryPoint : MonoBehaviour
{
    [Header("Scene Wiring")]
    [SerializeField] KoreographyChartLoader chartLoader;
    [SerializeField] RhythmConductor        conductor;
    [SerializeField] AudioSource            musicSource;

    [Header("Intro Sequence")]
    [SerializeField] Animator curtainAnimator;
    [SerializeField] string   curtainTrigger = "Open";
    [SerializeField] float    curtainSeconds = 3f;
    [SerializeField] float    prerollSeconds = 2f;

    [Header("Outro")]
    [SerializeField] float outroHoldSeconds = 3f;     // <- 3s breathe out
    [SerializeField] float fadeOutSeconds   = 0.6f;   // <- fade length
    [SerializeField] CanvasGroup fadeOverlay;         // assign your existing black overlay (alpha 0)

    bool ending;   // prevent double-outro

    void Awake()
    {
        var req = SceneFlow.PendingRhythm;
        if (req == null || req.song == null) return;

        var song = req.song;

        if (!musicSource) musicSource = FindFirstObjectByType<AudioSource>();
        if (!conductor)   conductor   = FindFirstObjectByType<RhythmConductor>();
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
    }
    void OnDisable()
    {
        if (conductor) conductor.SongFinished -= HandleSongFinished;
    }

    void HandleSongFinished()
    {
        if (ending) return;
        ending = true;

        // 1) Lock conductor so Update() can't re-arm
        conductor?.LockEnd();          // stops and sets finished=true
        musicSource?.Stop();

        // 2) Stop graph redraws immediately
        var path = FindFirstObjectByType<KnobPathRenderer>();
        if (path) path.enabled = false;

        StartCoroutine(CoOutro());
    }
    
    IEnumerator CoOutro()
    {
        // Freeze gameplay (you already do this)
        foreach (var b in FindObjectsByType<ButtonLaneController>(FindObjectsSortMode.None))
        { b.enabled = false; b.ClearAll(); }
        foreach (var k in FindObjectsByType<KnobLaneController>(FindObjectsSortMode.None))
        { k.enabled = false; k.ClearAll(); }
        if (conductor) conductor.enabled = false;

        // Hold on the last frame
        yield return new WaitForSecondsRealtime(outroHoldSeconds); // e.g., 3f

        // Global fade to black using the singleton
        if (Fade.Instance != null)
            yield return Fade.Instance.Out(fadeOutSeconds); // e.g., 0.6f

        // Build result + return (tell SceneFlow we already faded)
        var req = SceneFlow.PendingRhythm;
        var result = new SceneFlow.RhythmResult {
            song   = req?.song,
            origin = req?.origin ?? SceneFlow.RhythmOrigin.SongSelect,
            score  = 0,
            cleared= true
        };
        _ = SceneFlow.ReturnFromRhythmAsync(result, alreadyFaded: true);
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
}
