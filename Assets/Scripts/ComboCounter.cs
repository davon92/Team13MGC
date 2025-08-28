using UnityEngine;
using TMPro;
using System.Collections;

public class ComboCounter : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] ScoreTracker tracker;
    [SerializeField] TMP_Text     label;
    [SerializeField] RectTransform scaleTarget;

    [Header("Behavior")]
    [SerializeField] int   showFrom      = 2;        // show only at 2+
    [SerializeField] float pulseScale    = 1.2f;
    [SerializeField] float pulseUpTime   = 0.10f;
    [SerializeField] float pulseDownTime = 0.08f;
    [SerializeField] Color baseColor     = Color.white;

    [SerializeField] string suffix = " COMBO";       // -> "123 COMBO"
    string ComboText(int v) => $"{v}{suffix}";

    int       lastCombo = 0;
    Coroutine anim;

    void Awake()
    {
        if (!tracker)     tracker     = FindFirstObjectByType<ScoreTracker>();
        if (!label)       label       = GetComponent<TMP_Text>();
        if (!scaleTarget) scaleTarget = transform as RectTransform;

        if (label)
        {
            label.text    = "0" + suffix; // "0 COMBO"
            label.color   = baseColor;
            label.enabled = false;
        }
        scaleTarget.localScale = Vector3.one;
    }

    void OnEnable()
    {
        if (tracker)
        {
            tracker.OnComboChanged -= HandleComboChanged; // safety
            tracker.OnComboChanged += HandleComboChanged;
        }
    }
    void OnDisable()
    {
        if (tracker) tracker.OnComboChanged -= HandleComboChanged;
    }

    void HandleComboChanged(int combo)
    {
        if (!label) return;

        if (combo == 0)
        {
            if (anim != null) { StopCoroutine(anim); anim = null; }
            label.enabled = false;
            label.text = ComboText(0);
            scaleTarget.localScale = Vector3.one;
            lastCombo = 0;
            return;
        }

        if (combo >= showFrom && !label.enabled)
            label.enabled = true;

        label.text = ComboText(combo);

        if (combo != lastCombo)
            Pulse();

        lastCombo = combo;
    }

    void Pulse()
    {
        if (anim != null) StopCoroutine(anim);
        anim = StartCoroutine(CoPulse());
    }

    IEnumerator CoPulse()
    {
        var t = 0f;
        var startS = Vector3.one;
        var midS   = Vector3.one * pulseScale;

        while (t < pulseUpTime)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / Mathf.Max(0.0001f, pulseUpTime));
            scaleTarget.localScale = Vector3.Lerp(startS, midS, a);
            yield return null;
        }

        t = 0f;
        while (t < pulseDownTime)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / Mathf.Max(0.0001f, pulseDownTime));
            scaleTarget.localScale = Vector3.Lerp(midS, Vector3.one, a);
            yield return null;
        }

        scaleTarget.localScale = Vector3.one;
        anim = null;
    }
}
