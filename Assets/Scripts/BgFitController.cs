using UnityEngine;
using UnityEngine.UI;

public class BgFitController : MonoBehaviour {
    public enum FitMode { Cover, Contain }
    public FitMode fitMode = FitMode.Cover;
    [SerializeField] Image img;

    void Reset(){ img = GetComponent<Image>(); }
    public void SetSprite(Sprite s){ if (!img) img = GetComponent<Image>(); img.sprite = s; Apply(); }

    public void Apply() {
        if (!img || !img.sprite) return;

        var rtParent = (RectTransform)img.rectTransform.parent;
        var screen   = rtParent.rect;
        var spr      = img.sprite.rect;

        float wr = screen.width  / spr.width;
        float hr = screen.height / spr.height;
        float k  = (fitMode == FitMode.Cover) ? Mathf.Max(wr, hr) : Mathf.Min(wr, hr);

        var rt = img.rectTransform;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(spr.width * k, spr.height * k);
        rt.anchoredPosition = Vector2.zero;
    }
}