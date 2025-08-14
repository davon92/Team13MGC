using UnityEngine;
using TMPro;

public class JudgementPopup : MonoBehaviour
{
    [SerializeField] TMP_Text label;
    [SerializeField] float hold = 0.1f, fade = 0.15f;

    public void Show(Judgement j){
        StopAllCoroutines();
        gameObject.SetActive(true);
        label.text = j.ToString().ToUpperInvariant();
        StartCoroutine(Co());
    }

    System.Collections.IEnumerator Co(){
        var c = label.color; c.a = 1f; label.color = c;
        yield return new WaitForSecondsRealtime(hold);
        float t = 0f; while (t < fade){ t += Time.unscaledDeltaTime; c.a = 1f - (t/fade); label.color = c; yield return null; }
        gameObject.SetActive(false);
    }
}