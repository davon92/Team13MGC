using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// Draws moving KNOB ribbons inside a UI lane.
[ExecuteAlways]
[RequireComponent(typeof(CanvasRenderer))]
public class KnobPathRenderer : MaskableGraphic
{
    [Header("Timing / Layout")]
    [SerializeField] RhythmConductor conductor;
    [SerializeField] RectTransform laneRect;                 // same rect the stick sits in
    [SerializeField] float noteSpeedPxPerSec = 800f;         // match button lane/grid
    [SerializeField] bool  rightToLeft = true;

    [Header("Look")]
    [SerializeField] float thickness = 32f;                  // base thickness (px)
    [SerializeField] float stepPixels = 16f;                 // sampling step (smaller = smoother)
    [SerializeField] Color pulseTint = Color.white;          // flash towards this colour
    [SerializeField, Range(0,1)] float pulseAmount = 0.65f;  // strength of flash
    [SerializeField] float colorPulseHz = 6f;                // flash speed (Hz)
    [SerializeField] float highlightBeats = 2f;              // only pulse near hit line

    [Header("Culling window (beats)")]
    [SerializeField] int lookAheadBeats = 12;
    [SerializeField] int lookBehindBeats = 2;

    [Header("On-target width pulse")]
    [SerializeField] RectTransform thicknessFrom;            // e.g. LeftStick icon (diameter)
    [SerializeField] KnobLaneController lane;                // to read current tracking error
    [SerializeField, Tooltip("Extra width % added while on target")] 
    float pulseWhenOnTarget = 0.35f;
    [SerializeField] float pulseHz = 5f;

    readonly List<RhythmTypes.KnobSpan> spans = new();
    Color _baseColor;

    void Awake() => _baseColor = color;

    protected override void OnEnable()
    {
        base.OnEnable();
        _baseColor = color;          // remember inspector colour
        SetVerticesDirty();
    }

    public void SetSpans(List<RhythmTypes.KnobSpan> newSpans)
    {
        spans.Clear();
        if (newSpans != null) spans.AddRange(newSpans);
        SetVerticesDirty();
    }

    void LateUpdate()
    {
        if (isActiveAndEnabled) SetVerticesDirty(); // scroll with time
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (!conductor || !laneRect || spans.Count == 0) return;

        // Lane rect
        Rect r = laneRect.rect;

        // Visual time & bounds
        int sr  = Mathf.Max(1, conductor.SampleRate);
        int spb = Mathf.Max(1, conductor.SamplesPerBeat);
        int now = conductor.NowForVisual;
        int left  = now - lookBehindBeats * spb;
        int right = now + lookAheadBeats  * spb;

        // Sample step from pixels
        int stepSmp = Mathf.Max(1, Mathf.RoundToInt(stepPixels / Mathf.Max(1f, noteSpeedPxPerSec) * sr));

        // Live thickness (icon diameter), plus a soft pulse while tracing
        float baseThick = thickness;
        if (thicknessFrom) baseThick = Mathf.Max(thicknessFrom.rect.width, thicknessFrom.rect.height);

        bool onTarget = lane && lane.TryGetVisualError(out var err) && err <= lane.GoodWindow;
        float thickPulse = onTarget ? (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2f * Mathf.PI * pulseHz)) : 0f;
        float thickNow   = Mathf.Max(1f, baseThick * (1f + pulseWhenOnTarget * thickPulse));

        // Colour pulse ONLY near the hit line and ONLY while on target.
        Color baseCol = _baseColor;                                    // never touch alpha globally
        Color toCol   = new Color(pulseTint.r, pulseTint.g, pulseTint.b, baseCol.a);
        float tPulse  = onTarget ? (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2f * Mathf.PI * colorPulseHz)) : 0f;
        Color pulsed  = Color.Lerp(baseCol, toCol, pulseAmount * tPulse);
        int halfHi    = Mathf.RoundToInt(Mathf.Max(0f, highlightBeats) * 0.5f * spb);

        // Helpers
        float SampleToX(int smp)
        {
            float secs = (smp - now) / (float)sr;
            float px   = secs * noteSpeedPxPerSec;
            return rightToLeft ? r.xMin + px : r.xMax - px;
        }
        float pad = thickNow;
        float NormToY(float t) => Mathf.Lerp(r.yMin + pad, r.yMax - pad, Mathf.Clamp01(t));

        foreach (var span in spans)
        {
            if (span.endSample   < left)  continue;
            if (span.startSample > right) continue;

            int s0 = Mathf.Max(span.startSample, left);
            int s1 = Mathf.Min(span.endSample,   right);
            if (s1 <= s0) continue;

            int   prev = s0;
            float px0  = SampleToX(prev);
            float py0  = NormToY(span.targetAtSample(prev));

            for (int smp = s0 + stepSmp; smp <= s1; smp += stepSmp)
            {
                int   s  = (smp > s1) ? s1 : smp;
                float px = SampleToX(s);
                float py = NormToY(span.targetAtSample(s));

                bool inHighlight = (highlightBeats <= 0f) ? onTarget
                                   : onTarget && Mathf.Abs(s - now) <= halfHi;

                AddQuad(vh, px0, py0, px, py, thickNow, (Color32)(inHighlight ? pulsed : baseCol));
                px0 = px; py0 = py;
            }
        }
    }

    // Build a thin quad between (x0,y0)->(x1,y1)
    static void AddQuad(VertexHelper vh, float x0, float y0, float x1, float y1, float thick, Color32 col)
    {
        Vector2 a = new(x0, y0), b = new(x1, y1);
        Vector2 dir = b - a;
        if (dir.sqrMagnitude < 0.0001f) return;

        Vector2 n = Vector2.Perpendicular(dir).normalized * (thick * 0.5f);
        Vector3 p0 = a - n, p1 = a + n, p2 = b + n, p3 = b - n;

        int i = vh.currentVertCount;
        var v0 = UIVertex.simpleVert; v0.position = p0; v0.color = col;
        var v1 = UIVertex.simpleVert; v1.position = p1; v1.color = col;
        var v2 = UIVertex.simpleVert; v2.position = p2; v2.color = col;
        var v3 = UIVertex.simpleVert; v3.position = p3; v3.color = col;

        vh.AddVert(v0); vh.AddVert(v1); vh.AddVert(v2); vh.AddVert(v3);
        vh.AddTriangle(i + 0, i + 1, i + 2);
        vh.AddTriangle(i + 2, i + 3, i + 0);
    }
}
