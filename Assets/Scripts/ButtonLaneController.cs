using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class ButtonLaneController : MonoBehaviour
{
    [SerializeField] RhythmConductor conductor;
    [SerializeField] RectTransform laneRect;   // full-width rect of the lane UI
    [SerializeField] float noteSpeedPxPerSec = 800f;
    [SerializeField] Transform noteParent;
    [SerializeField] NoteObject notePrefab;
    public System.Action<Judgement> OnJudged;

    [Header("Input")]
    [SerializeField] private PlayerInput playerInput; 
    // If you’re using PlayerInput/InputActionReference, replace these with that pattern.
    InputAction btnA, btnY, btnB, btnX;

    // Hit windows (recomputed at runtime)
    [SerializeField] int perfectSamples = 735;    // placeholder; replaced in Start
    [SerializeField] int greatSamples   = 1610;
    [SerializeField] int goodSamples    = 2760;
    [SerializeField] RectTransform hitLine;
    [SerializeField] float spawnPaddingPx = 40f;
    [SerializeField] float despawnPaddingPx = 60f;

    readonly Queue<RhythmTypes.ButtonNote> upcoming = new();
    readonly List<NoteObject> active = new();

    float spawnX, hitX, despawnX;
    int Stravel;

    void OnEnable()
    {
        // Subscribe and Enable here (paired with OnDisable)
        btnA.performed += OnA; btnA.Enable();
        btnY.performed += OnY; btnY.Enable();
        btnB.performed += OnB; btnB.Enable();
        btnX.performed += OnX; btnX.Enable();
        Debug.Log($"[Buttons] enabled. A binds={btnA.bindings.Count}");
    }

    void OnDisable()
    {
        btnA.performed -= OnA; btnA.Disable();
        btnY.performed -= OnY; btnY.Disable();
        btnB.performed -= OnB; btnB.Disable();
        btnX.performed -= OnX; btnX.Disable();
    }
    
    void Awake()
    {
        var map = playerInput.actions.FindActionMap("Gameplay", throwIfNotFound: true);
        btnA = map.FindAction("A", true);
        btnY = map.FindAction("Y", true);
        btnB = map.FindAction("B", true);
        btnX = map.FindAction("X", true);
    }

    void Start()
    {
        var parent = (RectTransform)noteParent;

        // lane left/right in parent's local space
        Vector3 leftWorld  = laneRect.TransformPoint(new Vector3(laneRect.rect.xMin, 0f, 0f));
        Vector3 rightWorld = laneRect.TransformPoint(new Vector3(laneRect.rect.xMax, 0f, 0f));
        float leftX  = parent.InverseTransformPoint(leftWorld).x;
        float rightX = parent.InverseTransformPoint(rightWorld).x;

        // hit the line center in parent's local space
        float hitCenterX = parent.InverseTransformPoint(hitLine.position).x;

        hitX     = hitCenterX;
        spawnX   = rightX + spawnPaddingPx;
        despawnX = leftX  - despawnPaddingPx;

        // time (in samples) it takes to travel from spawnX to hitX at your pixel speed
        Stravel = Mathf.RoundToInt(((spawnX - hitX) / noteSpeedPxPerSec) * conductor.SampleRate);

        // windows at the actual sample rate (tweak as you like)
        perfectSamples = MsToSamples(22);
        greatSamples   = MsToSamples(50);
        goodSamples    = MsToSamples(100);
    }

    int MsToSamples(float ms) => Mathf.RoundToInt(ms * 0.001f * conductor.SampleRate);

    public void LoadChart(IEnumerable<RhythmTypes.ButtonNote> notesSortedByTime)
    {
        upcoming.Clear();
        foreach (var n in notesSortedByTime) upcoming.Enqueue(n);
    }

    void Update()
    {
        int nowV = conductor.NowForVisual;  // for motion
        int nowH = conductor.NowForHit;     // for judging

        // If the song is finished: don't spawn anything new,
        // just move what's alive offscreen.
        if (conductor.IsFinished)
        {
            for (int i = active.Count - 1; i >= 0; --i)
            {
                var n = active[i];
                n.UpdatePosition(nowV, spawnX, noteSpeedPxPerSec, conductor.SampleRate, despawnX);
                if (n.Offscreen) Recycle(i);
            }
            return;
        }

        // Spawn as notes enter the travel window.
        while (upcoming.Count > 0 && upcoming.Peek().startSample <= nowV + Stravel)
        {
            var data = upcoming.Dequeue();
            var no = Spawn(data);
            active.Add(no);
        }

        // Move + judge
        for (int i = active.Count - 1; i >= 0; --i)
        {
            var n = active[i];

            // motion (visual timeline)
            n.UpdatePosition(nowV, spawnX, noteSpeedPxPerSec, conductor.SampleRate, despawnX);

            if (!n.Judged)
            {
                // 1) Immediately DIM once the note is late (crossed the line).
                if (nowH >= n.Data.startSample && !n.Dimmed)
                    n.ApplyDimLook();

                // 2) Commit a MISS when the late window fully expires.
                if (nowH > n.Data.startSample + goodSamples)
                {
                    n.Miss();                     // darkens instantly (Miss() now does that)
                    OnJudged?.Invoke(Judgement.Miss);
                }
            }

            // Recycle only when truly off the left edge.
            if (n.Offscreen) Recycle(i);
        }
    }


    NoteObject Spawn(RhythmTypes.ButtonNote data)
    {
        var no = NoteObject.Get(notePrefab, noteParent);

        // travel = time (in samples) from spawn to hit your configured speed.
        int spawnSample = data.startSample - Stravel;

        no.Activate(data, spawnSample, data.startSample);
        no.UpdatePosition(conductor.NowSample, spawnX, noteSpeedPxPerSec, conductor.SampleRate, despawnX);
        
        if (no.Offscreen) no.Recycle();
        
        no.SetStyleForButton(data.button); // if you have per-button coloring
        return no;
    }

    void OnA(InputAction.CallbackContext _) { Debug.Log("A!"); TryHit(RhythmTypes.FaceButton.A); }
    void OnY(InputAction.CallbackContext _) { Debug.Log("Y!"); TryHit(RhythmTypes.FaceButton.Y); }
    void OnB(InputAction.CallbackContext _) { Debug.Log("B!"); TryHit(RhythmTypes.FaceButton.B); }
    void OnX(InputAction.CallbackContext _) { Debug.Log("X!"); TryHit(RhythmTypes.FaceButton.X); }


    void TryHit(RhythmTypes.FaceButton button)
    {
        int nowH = conductor.NowForHit;

        NoteObject best = null; int bestAbs = int.MaxValue;
        for (int i = 0; i < active.Count; ++i)
        {
            var n = active[i];
            if (n.Judged || n.Data.button != button) continue;
            int d = Mathf.Abs(nowH - n.Data.startSample);
            if (d < bestAbs) { bestAbs = d; best = n; }
        }
        if (best == null || bestAbs > goodSamples) return;

        Judgement j =
            (bestAbs <= perfectSamples) ? Judgement.Perfect :
            (bestAbs <= greatSamples)   ? Judgement.Great   :
            Judgement.Good;

        OnJudged?.Invoke(j);
        best.Hit(j);
        active.Remove(best);
        best.Recycle(); // hits vanish immediately; missed notes slide out
    }


    void Recycle(int i)
    {
        active[i].Recycle();
        active.RemoveAt(i);
    }
    
    public void ClearAll()
    {
        // Despawn any active notes
        for (int i = active.Count - 1; i >= 0; --i)
            Recycle(i);

        upcoming.Clear();
    }
}
