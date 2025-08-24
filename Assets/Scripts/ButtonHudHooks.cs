using UnityEngine;

public class ButtonHudHooks : MonoBehaviour
{
    [SerializeField] ButtonLaneController lane;
    [SerializeField] HitLinePulse hitPulse;
    [SerializeField] JudgementPopup popup;   // optional (below)
    [SerializeField] HitBurst burst;
    
    void OnEnable(){
        if (lane != null){
            lane.OnJudged += OnJudge;
        }
    }
    void OnDisable(){
        if (lane != null){
            lane.OnJudged -= OnJudge;
        }
    }
    void OnJudge(Judgement j)
    {
        hitPulse?.Ping();
        popup?.Show(j);
        burst?.Play(j);
    }
}