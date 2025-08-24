using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum Judgement { Perfect, Great, Good, Miss }

public class NoteObject : MonoBehaviour
{
    [SerializeField] RectTransform rt;
    [SerializeField] Image img;

    [Header("Visuals")]
    [SerializeField] NoteStyleSet style;   // ← assign the asset here
    [SerializeField] bool setNativeSize = false;

    public RhythmTypes.ButtonNote Data { get; private set; }
    public bool Judged { get; private set; }
    public bool Offscreen { get; private set; }

    int spawnSample, hitSample;

    void Awake()
    {
        if (!rt)  rt  = GetComponent<RectTransform>();
        if (!img) img = GetComponent<Image>();
    }

    // --- simple pool ---
    static readonly Stack<NoteObject> pool = new();
    static void ResetStaticPool() => pool.Clear();
    public static NoteObject Get(NoteObject prefab, Transform parent)
    {
        NoteObject n = null;

        // Pop until we find a still-alive instance (skip destroyed/null)
        while (pool.Count > 0 && !n)
            n = pool.Pop();

        if (!n)
        {
            // Instantiate a fresh one from the prefab
            n = Instantiate(prefab, parent);
        }
        else
        {
            // Re-parent and reactivate a pooled one
            n.transform.SetParent(parent, false);
            n.gameObject.SetActive(true);
        }

        // Reset per-note flags/state here if you keep any
        n.Offscreen = false;
        n.Judged    = false;
        // n.ResetVisuals(); // (optional) whatever you need

        return n;
    }
    public void Recycle()
    {
        if (!this) return;                    // already destroyed
        gameObject.SetActive(false);
        transform.SetParent(null, false);
        pool.Push(this);
    }

    public void Activate(RhythmTypes.ButtonNote data, int spawnSample, int hitSample)
    {
        Data = data;
        this.spawnSample = spawnSample;
        this.hitSample   = hitSample;
    }

    public void SetStyleForButton(RhythmTypes.FaceButton b)
    {
        if (!img || !style) return;

        img.sprite = style.GetSprite(b);
        img.color  = style.GetColor(b);
        img.raycastTarget = false;

        //if (setNativeSize) img.SetNativeSize(); // or keep a fixed size via the RectTransform
    }

    public void UpdatePosition(int now, float spawnX, float hitX, int Stravel, float despawnX)
    {
        float t = Mathf.InverseLerp(spawnSample, hitSample, now);
        float x = Mathf.Lerp(spawnX, hitX, t);
        var p = rt.anchoredPosition; p.x = x; rt.anchoredPosition = p;
        if (x < despawnX) Offscreen = true;
    }

    public void Hit(Judgement j) { Judged = true; Recycle(); }
    public void Miss()           { Judged = true; Recycle(); }
}