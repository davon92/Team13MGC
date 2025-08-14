using UnityEngine;

public class ButtonHudHooks : MonoBehaviour
{
    [SerializeField] ButtonLaneController lane;
    [SerializeField] HitLinePulse hitPulse;
    [SerializeField] JudgementPopup popup;   // optional (below)

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
    void OnJudge(Judgement j){
        hitPulse?.Ping();
        popup?.Show(j); // if you add the popup
    }
}