using UnityEngine;
using TMPro;
using System.Collections;

public class ComboCounter : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] ScoreTracker tracker;         // auto-finds if left empty
    [SerializeField] TMP_Text     label;           // put your TMP text here
    [SerializeField] RectTransform scaleTarget;    // defaults to this transform

    [Header("Behavior")]
    [SerializeField] int   showFrom    = 2;        // show only at 2+
    [SerializeField] float pulseScale  = 1.2f;
    [SerializeField] float pulseUpTime = 0.10f;
    [SerializeField] float pulseDownTime = 0.08f;
    [SerializeField] Color baseColor   = Color.white;
    [SerializeField] string suffix     = " COMBO";

    int lastCombo = 0;
    Coroutine anim;

    void Awake()
    {
        if (!tracker)     tracker     = FindFirstObjectByType<ScoreTracker>();
        if (!label)       label       = GetComponent<TMP_Text>();
        if (!scaleTarget) scaleTarget = transform as RectTransform;

        // start hidden
        if (label) {
            label.text   = "0" + suffix;
            label.color  = baseColor;
            label.enabled = false;
        }
        scaleTarget.localScale = Vector3.one;
    }

    void OnEnable()
    {
        if (tracker) tracker.OnComboChanged += HandleComboChanged;
    }

    void OnDisable()
    {
        if (tracker) tracker.OnComboChanged -= HandleComboChanged;
    }

    void HandleComboChanged(int combo, int _maxCombo)
    {
        if (!label) return;

        if (combo == 0)
        {
            // break: reset and hide immediately
            if (anim != null) { StopCoroutine(anim); anim = null; }
            label.text = "0" + suffix;
            label.enabled = false;
            scaleTarget.localScale = Vector3.one;
            lastCombo = 0;
            return;
        }

        // show at threshold
        if (combo >= showFrom && !label.enabled)
            label.enabled = true;

        // update text
        label.text = combo.ToString() + suffix;

        // pulse on increase
        if (combo > lastCombo && combo >= showFrom)
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
        // scale/color up
        var t = 0f;
        var startCol = label.color;
        var startS   = Vector3.one;
        var midS     = Vector3.one * pulseScale;

        while (t < pulseUpTime)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / pulseUpTime);
            label.color = Color.Lerp(startCol, baseColor, a);   // keep color stable
            scaleTarget.localScale = Vector3.Lerp(startS, midS, a);
            yield return null;
        }

        // settle back
        t = 0f;
        while (t < pulseDownTime)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / pulseDownTime);
            scaleTarget.localScale = Vector3.Lerp(midS, Vector3.one, a);
            yield return null;
        }

        label.color = baseColor;
        scaleTarget.localScale = Vector3.one;
        anim = null;
    }
}
