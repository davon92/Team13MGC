using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class BeatGridScroller : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField] private RhythmConductor conductor;
    [SerializeField] private float bpm = 120f;

    [Header("Visual speed (must match your notes)")]
    [SerializeField] private float noteSpeedPxPerSec = 800f;

    [Header("Tile options")]
    [SerializeField] private float beatsPerTile = 1f;
    [SerializeField] private float phaseBeats = 0f;
    [SerializeField] private bool rightToLeft = true;

    [Header("Lane area (to compute tiling)")]
    [SerializeField] private RectTransform laneRect; // usually NotesParent
    
    [SerializeField] RectTransform hitLine;     // assign same HitLine
    private RawImage raw;
    private float tilesAcross;

    void Awake()
    {
        // Lazy init also happens inside Recompute in case this runs first in Editor
        raw = GetComponent<RawImage>();
    }

    void Start()
    {
        RecomputeMetrics();
        if (hitLine)
        {
            // how many pixels from lane left to hit line?
            var parent = laneRect;
            float leftX  = parent.InverseTransformPoint(laneRect.TransformPoint(new Vector3(laneRect.rect.xMin,0,0))).x;
            float hitX   = parent.InverseTransformPoint(hitLine.position).x;
            float offsetPx = hitX - leftX;

            float pixelsPerBeat = noteSpeedPxPerSec * (60f / bpm);
            phaseBeats = -offsetPx / Mathf.Max(1e-4f, pixelsPerBeat); // move the texture so a tick sits on the hit line
        }
    }

    void OnRectTransformDimensionsChange() => RecomputeMetrics();

    public void SetNoteSpeed(float pxPerSec)
    {
        noteSpeedPxPerSec = Mathf.Max(1f, pxPerSec);
        RecomputeMetrics();
    }

    public void SetBpm(float newBpm, float newPhaseBeats = 0f)
    {
        bpm = Mathf.Max(1f, newBpm);
        phaseBeats = newPhaseBeats;
        RecomputeMetrics();
    }

    void RecomputeMetrics()
    {
        if (raw == null) raw = GetComponent<RawImage>();
        // lane rect fallback to *this* rect if none assigned
        RectTransform rt = laneRect ? laneRect : transform as RectTransform;
        if (raw == null || rt == null) return;                     // not ready yet

        var r = rt.rect;
        if (r.width <= 1f || bpm <= 0f || noteSpeedPxPerSec <= 0f) // nothing to compute
            return;

        float pixelsPerBeat = noteSpeedPxPerSec * (60f / bpm);
        float pixelsPerTile = pixelsPerBeat * Mathf.Max(0.0001f, beatsPerTile);

        tilesAcross = Mathf.Max(1f, r.width / pixelsPerTile);

        var uv = raw.uvRect;
        uv.width = tilesAcross;
        uv.height = 1f;
        raw.uvRect = uv;
    }

    void LateUpdate()
    {
        if (conductor == null || raw == null) return;
        float seconds = conductor.NowForVisual / (float)conductor.SampleRate; // was NowSample
        float beats   = seconds * (bpm / 60f);
        float tileOffset = (beats + phaseBeats) / Mathf.Max(0.0001f, beatsPerTile);

        var uv = raw.uvRect;
        uv.x = rightToLeft ? +tileOffset : -tileOffset;
        raw.uvRect = uv;
    }
}
