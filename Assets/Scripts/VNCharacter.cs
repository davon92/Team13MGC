// VNCharacter.cs
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
public class VNCharacter : MonoBehaviour {
    public CharacterDef def;
    [SerializeField] Image image;
    CanvasGroup cg;
    RectTransform rt;

    void Awake() {
        rt = (RectTransform)transform;
        cg = GetComponent<CanvasGroup>();
        if (!image) image = gameObject.AddComponent<Image>();
        image.preserveAspect = true;
        cg.alpha = 0f;
    }

    public void SetExpression(string expr)
    {
        if (!def) return;

        var spr = def.Get(expr);
        if (!spr) return;

        image.sprite = spr;
        image.preserveAspect = true;
        image.SetNativeSize();                     // <- makes 1 UI unit = sprite pixels
        rt.pivot = new Vector2(0.5f, 0f);          // optional: “feet on baseline”
    }

    public void SetFlip(bool flip) {
        var s = rt.localScale; s.x = Mathf.Abs(s.x) * (flip ? -1f : 1f); rt.localScale = s;
    }

    public RectTransform RT => rt;
    public CanvasGroup CG => cg;
}