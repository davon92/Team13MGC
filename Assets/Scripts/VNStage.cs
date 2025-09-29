// VNStage.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if DOTWEEN_EXISTS || DOTWEEN_TMP
using DG.Tweening;
#endif

public class VNStage : MonoBehaviour {
    [Header("Stage root & slots")]
    public RectTransform stageRoot;
    public RectTransform left, midLeft, center, midRight, right;
    public enum FitMode { Cover, Contain }
    [Header("Character prefabs/defs")]
    public VNCharacter characterPrefab;
    public List<CharacterDef> characterDefs = new();

    [Header("Background")]
    public UnityEngine.UI.Image backgroundImage;
    public UnityEngine.Video.VideoPlayer backgroundVideo; // optional
    public BackgroundDatabase bgDatabase;   

    readonly Dictionary<string, VNCharacter> _live = new();
    readonly Dictionary<string, CharacterDef> _defs = new();
    [SerializeField] FitMode backgroundFit = FitMode.Cover;   // <— NEW
    void Awake() {
        foreach (var d in characterDefs) if (d && !_defs.ContainsKey(d.id)) _defs.Add(d.id, d);
    }

    public bool TryGetDef(string id, out CharacterDef d) => _defs.TryGetValue(id, out d);

    public RectTransform Slot(string name) {
        switch (name.ToLowerInvariant()) {
            case "left":       return left;
            case "midleft":
            case "ml":         return midLeft;
            case "center":
            case "mid":        return center;
            case "midright":
            case "mr":         return midRight;
            case "right":      return right;
            default:           return center;
        }
    }

    public IEnumerator CoShow(string id, string expr, string atSlot, float dur, bool flip=false) {
        if (!_live.TryGetValue(id, out var ch)) {
            if (!TryGetDef(id, out var def)) yield break;
            ch = Instantiate(characterPrefab, stageRoot);
            ch.def = def;
            ch.name = $"CHAR_{id}";
            _live.Add(id, ch);
        }
        ch.SetExpression(expr);
        ch.SetFlip(flip);

        var target = Slot(atSlot);
        if (target) ch.RT.position = target.position;

#if DOTWEEN_EXISTS || DOTWEEN_TMP
        ch.CG.DOFade(1f, dur);
        yield return new WaitForSeconds(dur);
#else
        yield return Fade(ch.CG, ch.CG.alpha, 1f, dur);
#endif
    }

    public IEnumerator CoHide(string id, float dur) {
        if (!_live.TryGetValue(id, out var ch)) yield break;
#if DOTWEEN_EXISTS || DOTWEEN_TMP
        ch.CG.DOFade(0f, dur);
        yield return new WaitForSeconds(dur);
#else
        yield return Fade(ch.CG, ch.CG.alpha, 0f, dur);
#endif
        Destroy(ch.gameObject);
        _live.Remove(id);
    }

    public IEnumerator CoExpr(string id, string expr, float dur=0f) {
        if (!_live.TryGetValue(id, out var ch)) yield break;
        ch.SetExpression(expr);
        if (dur > 0f) yield return new WaitForSeconds(dur);
    }

    public IEnumerator CoFlip(string id, bool flip) {
        if (_live.TryGetValue(id, out var ch)) ch.SetFlip(flip);
        yield break;
    }

    public IEnumerator CoMove(string id, string toSlot, float dur) {
        if (!_live.TryGetValue(id, out var ch)) yield break;
        var target = Slot(toSlot);
        if (!target) yield break;
#if DOTWEEN_EXISTS || DOTWEEN_TMP
        ch.RT.DOMove(target.position, dur).SetEase(Ease.InOutSine);
        yield return new WaitForSeconds(dur);
#else
        Vector3 start = ch.RT.position, end = target.position;
        float t = 0f;
        while (t < dur) { t += Time.deltaTime; ch.RT.position = Vector3.Lerp(start, end, Mathf.SmoothStep(0,1,t/dur)); yield return null; }
        ch.RT.position = end;
#endif
    }

    public IEnumerator CoBG(string idOrIdColonVariant, float dur, string variantKey = null)
    {
        if (!bgDatabase) yield break;

        // Allow "alley:night" shorthand if variantKey wasn't passed
        if (string.IsNullOrEmpty(variantKey))
        {
            int idx = idOrIdColonVariant.IndexOf(':');
            if (idx >= 0)
            {
                variantKey = idOrIdColonVariant[(idx + 1)..];
                idOrIdColonVariant = idOrIdColonVariant[..idx];
            }
        }

        if (!bgDatabase.TryGet(idOrIdColonVariant, out var def))
        {
            Debug.LogWarning($"[VNStage] BG id '{idOrIdColonVariant}' not found");
            yield break;
        }

        // Your BackgroundDef should return the matching assets:
        // (Sprite sprite, VideoClip video) Resolve(string variant)
        var (sprite, video) = def.Resolve(variantKey);

        // --- Your existing crossfade logic kept as-is ---
        if (backgroundVideo && video != null)
        {
            yield return FadeBG(0f, dur * 0.5f);
            backgroundVideo.clip = video;
            backgroundVideo.gameObject.SetActive(true);
            backgroundVideo.isLooping = true;
            backgroundVideo.Play();
            if (backgroundImage) backgroundImage.enabled = false;
            yield return FadeBG(1f, dur * 0.5f);
        }
        else if (backgroundImage && sprite != null)
        {
            yield return FadeBG(0f, dur * 0.5f);
            if (backgroundVideo)
            {
                backgroundVideo.Stop();
                backgroundVideo.gameObject.SetActive(false);
            }
            backgroundImage.enabled = true;
            backgroundImage.sprite = sprite;

            // Make the assigned sprite respect its native pixels and fit the stage.
            ApplyBgFit(backgroundImage, sprite);

            yield return FadeBG(1f, dur * 0.5f);
        }
    }
    
    void ApplyBgFit(UnityEngine.UI.Image img, Sprite sp)
    {
        if (!img || !sp) return;

        // Pixel size of the sprite
        float sw = sp.rect.width;
        float sh = sp.rect.height;

        // Size of the stage area we want to fill
        var container = stageRoot ? stageRoot : img.rectTransform.parent as RectTransform;
        var cr = container.rect;
        float cw = cr.width, ch = cr.height;

        // Use your current mode. If you already store a FitMode in VNStage, keep that.
        float cover = Mathf.Max(cw / sw, ch / sh);   // fill (crop if needed)
        float contain = Mathf.Min(cw / sw, ch / sh); // letterbox (no crop)

        // Pick one – or if you already have an enum/field, swap this line to match it.
        float scale = cover;

        img.preserveAspect = true;
        img.rectTransform.sizeDelta = new Vector2(sw * scale, sh * scale);
    }

    IEnumerator FadeBG(float target, float dur) {
        if (!backgroundImage && !backgroundVideo) yield break;
        var cg = (backgroundImage ? backgroundImage.GetComponent<CanvasGroup>() : null)
                 ?? (backgroundVideo ? backgroundVideo.GetComponent<CanvasGroup>() : null)
                 ?? (backgroundImage ? backgroundImage.gameObject.AddComponent<CanvasGroup>()
                     : backgroundVideo.gameObject.AddComponent<CanvasGroup>());
        float start = cg.alpha, t = 0f;
        while (t < dur) { t += Time.deltaTime; cg.alpha = Mathf.Lerp(start, target, t/dur); yield return null; }
        cg.alpha = target;
    }

    IEnumerator Fade(CanvasGroup cg, float a, float b, float dur) {
        if (dur <= 0f) { cg.alpha = b; yield break; }
        float t = 0f;
        while (t < dur) { t += Time.deltaTime; cg.alpha = Mathf.Lerp(a, b, t/dur); yield return null; }
        cg.alpha = b;
    }
}
