using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class KnobLaneController : MonoBehaviour
{
    [SerializeField] RhythmConductor conductor;

    [Header("Lane visuals")]
    [SerializeField] RectTransform stickParent;   // Rect spanning the bottom lane
    [SerializeField] RectTransform targetDot;     // dot at hit line (shows target)
    [SerializeField] RectTransform playerDot;     // dot at hit line (shows stick)
    Rect knobRect; bool haveRect;

    [Header("Input")]
    [SerializeField] private PlayerInput playerInput;
    InputAction leftStick;

    [Header("Grading")]
    [SerializeField] float perfectErr = 0.03f;
    [SerializeField] float greatErr   = 0.06f;
    [SerializeField] float goodErr    = 0.10f;

    [Header("Grace (milliseconds)")]
    [SerializeField] float enterGraceMs = 60f;
    [SerializeField] float exitGraceMs  = 80f;

    readonly Queue<RhythmTypes.KnobSpan> spans = new();
    RhythmTypes.KnobSpan? active;
    float sumScore, maxScore;
    int enterGraceSamples, exitGraceSamples;

    void Awake()
    {
        var map = playerInput.actions.FindActionMap("Gameplay", true);
        leftStick = map.FindAction("LeftStick", true);
    }

    void OnEnable()  { leftStick.Enable(); }
    void OnDisable() { leftStick.Disable(); }

    void Start()
    {
        int sr = conductor.SampleRate;
        enterGraceSamples = Mathf.RoundToInt(enterGraceMs * 0.001f * sr);
        exitGraceSamples  = Mathf.RoundToInt(exitGraceMs  * 0.001f * sr);
        if (stickParent) { knobRect = stickParent.rect; haveRect = true; }
        if (targetDot) targetDot.gameObject.SetActive(false);
    }

    public void LoadChart(IEnumerable<RhythmTypes.KnobSpan> segments)
    {
        spans.Clear();
        foreach (var s in segments) spans.Enqueue(s);
        active = null;
        if (targetDot) targetDot.gameObject.SetActive(false);
    }

    float YFrom01(float v01) => !haveRect ? 0f
        : Mathf.Lerp(knobRect.yMin, knobRect.yMax, Mathf.Clamp01(v01));

    void Update()
    {
        int nowV = conductor.NowForVisual;

        if (!active.HasValue && spans.Count > 0 && spans.Peek().startSample <= nowV)
        {
            active = spans.Dequeue();
            sumScore = 0f; maxScore = 0f;
            if (targetDot) targetDot.gameObject.SetActive(true);
        }

        if (!active.HasValue)
        {
            // just move player dot
            var v = leftStick.ReadValue<Vector2>();
            if (playerDot) { var p = playerDot.anchoredPosition; p.y = YFrom01(Mathf.InverseLerp(-1f,1f,v.y)); playerDot.anchoredPosition = p; }
            return;
        }

        var s = active.Value;
        int nowH = conductor.NowForHit;

        if (nowH > s.endSample + exitGraceSamples)
        {
            FinalizeSpan(sumScore, maxScore);
            active = null;
            if (targetDot) targetDot.gameObject.SetActive(false);
            return;
        }

        // visuals
        float target01V = Mathf.Clamp01(s.targetAtSample(nowV));
        float stick01   = Mathf.InverseLerp(-1f, 1f, leftStick.ReadValue<Vector2>().y);
        if (haveRect)
        {
            if (targetDot) { var p = targetDot.anchoredPosition; p.y = YFrom01(target01V); targetDot.anchoredPosition = p; }
            if (playerDot) { var p = playerDot.anchoredPosition; p.y = YFrom01(stick01);  playerDot.anchoredPosition = p; }
        }

        // grading window on HIT time
        if (nowH >= s.startSample - enterGraceSamples && nowH <= s.endSample)
        {
            float target01H = Mathf.Clamp01(s.targetAtSample(nowH));
            float err = Mathf.Abs(target01H - stick01);
            float tick = (err <= perfectErr) ? 1f : (err <= greatErr) ? 0.7f : (err <= goodErr) ? 0.4f : 0f;
            sumScore += tick; maxScore += 1f;
        }
    }


    void FinalizeSpan(float sum, float max)
    {
        float r = (max > 0f) ? (sum / max) : 0f;
        // TODO: send to score/combo; for now just log:
        if      (r >= 0.90f) Debug.Log("[Knob] PERFECT");
        else if (r >= 0.75f) Debug.Log("[Knob] GREAT");
        else if (r >= 0.55f) Debug.Log("[Knob] GOOD");
        else                 Debug.Log("[Knob] MISS");
    }
}
