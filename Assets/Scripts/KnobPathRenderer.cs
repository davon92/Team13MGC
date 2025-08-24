using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// Draws moving KNOB ribbons (SDVX/Trombone-style) inside a UI lane.
[ExecuteAlways]
[RequireComponent(typeof(CanvasRenderer))]
public class KnobPathRenderer : MaskableGraphic
{
    [Header("Timing / Layout")]
    [SerializeField] RhythmConductor conductor;
    [SerializeField] RectTransform laneRect;             // same rect the stick sits in
    [SerializeField] float noteSpeedPxPerSec = 800f;     // must match your button lane/grid
    [SerializeField] bool rightToLeft = true;

    [Header("Look")]
    [SerializeField] float thickness = 10f;              // ribbon thickness (px)
    [SerializeField] float stepPixels = 8f;              // sampling step along X (smaller = smoother)

    [Header("Culling window (beats)")]
    [SerializeField] int lookAheadBeats = 12;            // draw this many beats to the right
    [SerializeField] int lookBehindBeats = 2;            // and a little to the left

    List<RhythmTypes.KnobSpan> spans = new();
    
    [SerializeField] RectTransform thicknessFrom;     // ← e.g. the LeftStick icon (RectTransform)
    [SerializeField] KnobLaneController lane;        // ← read current tracking error
    [SerializeField] float pulseWhenOnTarget = 0.35f;// 35% extra thickness when tracing
    [SerializeField] float pulseHz = 5f;             // how fast the pulse animates

    public void SetSpans(List<RhythmTypes.KnobSpan> newSpans)
    {
        spans = (newSpans != null) ? newSpans : new List<RhythmTypes.KnobSpan>();
        SetVerticesDirty(); // force rebuild
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        SetVerticesDirty();
    }

    void LateUpdate()
    {
        // Rebuild every frame so the ribbon scrolls with time.
        if (isActiveAndEnabled) SetVerticesDirty();
    }

   protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (conductor == null || laneRect == null || spans == null || spans.Count == 0) return;

        // ----- Lane dimensions (avoid name clash with the 'lane' field) -----
        Rect laneR = laneRect.rect;
        float laneWidth  = laneR.width;
        float laneHeight = laneR.height;

        // ----- Time & conversions -----
        int nowSamples   = conductor.SampleTime;                   // visual time (samples)
        int spb          = Mathf.Max(1, conductor.SamplesPerBeat);
        int sr           = Mathf.Max(1, conductor.SampleRate);
        int aheadSamples = lookAheadBeats  * spb;
        int backSamples  = lookBehindBeats * spb;
        int leftBound    = nowSamples - backSamples;
        int rightBound   = nowSamples + aheadSamples;

        // Step along X as samples-per-step for the desired pixel step
        int stepSamples = Mathf.Max(1,
            Mathf.RoundToInt(stepPixels / Mathf.Max(1f, noteSpeedPxPerSec) * sr));

        // ----- Live thickness (auto-size to icon + pulse when tracing) -----
        float baseThickness = thickness;
        if (thicknessFrom)
        {
            var r = thicknessFrom.rect;
            baseThickness = Mathf.Max(r.width, r.height);          // icon diameter in px
        }

        float thicknessNow = baseThickness;
        if (lane && lane.TryGetVisualError(out var err) && err <= lane.GoodWindow)
        {
            // 0..1 sine pulse, UI-time (unscaled)
            float s = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2f * Mathf.PI * pulseHz);
            thicknessNow = baseThickness * (1f + pulseWhenOnTarget * s);
        }
        thicknessNow = Mathf.Max(1f, thicknessNow);                // safety clamp

        // Helpers
        float SampleToX(int sample)
        {
            float secs = (sample - nowSamples) / (float)sr;        // seconds from hit line
            float px   = secs * noteSpeedPxPerSec;
            return rightToLeft ? laneR.xMin + px : laneR.xMax - px;
        }

        float pad = thicknessNow;                                  // keep ribbon inside lane
        float NormToY(float t) =>
            Mathf.Lerp(laneR.yMin + pad, laneR.yMax - pad, Mathf.Clamp01(t));

        Color32 col = color;

        // ----- Draw all visible spans -----
        foreach (var span in spans)
        {
            if (span.endSample   < leftBound)  continue; // fully left of view
            if (span.startSample > rightBound) continue; // fully right of view

            int s0 = Mathf.Max(span.startSample, leftBound);
            int s1 = Mathf.Min(span.endSample,   rightBound);
            if (s1 <= s0) continue;

            int prevSample = s0;
            float prevX = SampleToX(prevSample);
            float prevY = NormToY(span.targetAtSample(prevSample));

            for (int s = s0 + stepSamples; s <= s1; s += stepSamples)
            {
                int sample = (s > s1) ? s1 : s;
                float x = SampleToX(sample);
                float y = NormToY(span.targetAtSample(sample));

                AddQuad(vh, prevX, prevY, x, y, thicknessNow, col);
                prevX = x; prevY = y;
            }
        }
    }


    // Build a thin quad between (x0,y0)->(x1,y1)
    void AddQuad(VertexHelper vh, float x0, float y0, float x1, float y1, float thick, Color32 col)
    {
        Vector2 a = new Vector2(x0, y0);
        Vector2 b = new Vector2(x1, y1);
        Vector2 dir = (b - a);
        if (dir.sqrMagnitude < 0.0001f) return;

        Vector2 n = Vector2.Perpendicular(dir).normalized * (thick * 0.5f);
        Vector3 p0 = a - n, p1 = a + n, p2 = b + n, p3 = b - n;

        int start = vh.currentVertCount;

        var v0 = UIVertex.simpleVert; v0.position = p0; v0.color = col; v0.uv0 = Vector2.zero;
        var v1 = UIVertex.simpleVert; v1.position = p1; v1.color = col; v1.uv0 = Vector2.right;
        var v2 = UIVertex.simpleVert; v2.position = p2; v2.color = col; v2.uv0 = Vector2.one;
        var v3 = UIVertex.simpleVert; v3.position = p3; v3.color = col; v3.uv0 = Vector2.up;

        vh.AddVert(v0); vh.AddVert(v1); vh.AddVert(v2); vh.AddVert(v3);
        vh.AddTriangle(start + 0, start + 1, start + 2);
        vh.AddTriangle(start + 2, start + 3, start + 0);
    }
}
