using UnityEngine;
using UnityEngine.UI;

public class HitLinePulse : MonoBehaviour
{
    [SerializeField] Image img;
    [SerializeField] Color pulse = new Color(1f, 1f, .4f, 1f);
    [SerializeField] float decay = 12f;
    Color baseCol; float t;

    void Awake(){ if(!img) img = GetComponent<Image>(); baseCol = img.color; }
    public void Ping(){ t = 1f; }
    void Update(){ if (t > 0f){ t = Mathf.MoveTowards(t, 0f, decay*Time.unscaledDeltaTime); img.color = Color.Lerp(baseCol, pulse, t); } }
}