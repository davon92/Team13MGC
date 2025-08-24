using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum Judgement { Perfect, Great, Good, Miss }

public class NoteObject : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] RectTransform rt;
    [SerializeField] Image img;

    [Header("Visuals")]
    [SerializeField] NoteStyleSet style;           // sprites/colors per button
    [SerializeField] bool setNativeSize = false;

    [Header("Miss Look")]
    [Range(0f, 1f)] [SerializeField] float missDim   = 0.35f; // RGB multiplier (0=black .. 1=unchanged)
    [Range(0f, 1f)] [SerializeField] float missAlpha = 0.65f; // alpha multiplier

    // State ----------------------------------------------------
    public RhythmTypes.ButtonNote Data { get; private set; }
    public bool Judged { get; private set; }
    public bool Offscreen { get; private set; }

    int _spawnSample;          // when the note became visible
    int _hitSample;            // when it should cross the hit line
    Color baseColor = Color.white;
    bool  missApplied;
    
    bool _dimmed;
    public bool Dimmed => _dimmed;
    
    public void ApplyDimLook()
    {
        if (_dimmed) return;
        if (img)
        {
            var c = baseColor;
            img.color = new Color(c.r * missDim, c.g * missDim, c.b * missDim, c.a * missAlpha);
        }
        _dimmed = true;
    }

    void Awake()
    {
        if (!rt)  rt  = GetComponent<RectTransform>();
        if (!img) img = GetComponent<Image>();
        baseColor = img ? img.color : Color.white;
    }

    // ---------- Simple pool ----------
    static readonly Stack<NoteObject> pool = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticPool() => pool.Clear();

    public static NoteObject Get(NoteObject prefab, Transform parent)
    {
        NoteObject n = null;
        while (pool.Count > 0 && !n) n = pool.Pop();

        if (!n)
            n = Instantiate(prefab, parent);
        else
        {
            n.transform.SetParent(parent, false);
            n.gameObject.SetActive(true);
        }

        n.Offscreen = false;
        n.Judged    = false;
        if (n.img) n.img.color = n.baseColor;   // ensure clean tint on reuse
        return n;
    }

    public void Recycle()
    {
        gameObject.SetActive(false);
        Judged = false;
        Offscreen = false;
        if (img) img.color = baseColor;
        pool.Push(this);
    }

    // ---------- API called by the lane ----------
    public void Activate(RhythmTypes.ButtonNote data, int spawnSampleIn, int hitSampleIn)
    {
        gameObject.SetActive(true);
        ResetLook();

        Data         = data;
        _spawnSample = spawnSampleIn;
        _hitSample   = hitSampleIn;
        Judged       = false;
        Offscreen    = false;

        SetStyleForButton(data.button);
    }

    // sprite + color from your NoteStyleSet (also caches baseColor)
    public void SetStyleForButton(RhythmTypes.FaceButton b)
    {
        if (!img || !style) return;

        img.sprite = style.GetSprite(b);
        if (setNativeSize) img.SetNativeSize();

        baseColor        = style.GetColor(b);
        img.color        = baseColor;
        img.raycastTarget = false;

        missApplied = false;
    }

    // Move by time/speed; keep sliding OFF the screen.
    // Lane should call this every frame until Offscreen becomes true.
    public void UpdatePosition(int nowSample, float spawnX, float pxPerSec, int sampleRate, float despawnX)
    {
        float secondsSinceSpawn = (nowSample - _spawnSample) / (float)sampleRate;
        float dx = pxPerSec * secondsSinceSpawn;

        // Right-to-left motion: start at spawnX and move left
        float x = spawnX - dx;

        var p = rt.anchoredPosition;
        p.x = x;
        rt.anchoredPosition = p;

        if (x <= despawnX) Offscreen = true;   // lane will Recycle()
    }

    public void Hit(Judgement j)
    {
        Judged = true;                         // lane will Recycle immediately
    }

    public void Miss()
    {
        if (Judged) return;
        Judged = true;

        ApplyDimLook();       // go dark right away

        // Do NOT recycle here; the lane will keep sliding it offscreen
        // and recycle once it’s past the despawnX.
    }

    public void ResetLook()
    {
        if (img) img.color = baseColor;
        _dimmed = false;
    }
}
