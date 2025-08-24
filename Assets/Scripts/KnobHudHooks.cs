using UnityEngine;

public class KnobHudHooks : MonoBehaviour
{
    [SerializeField] KnobLaneController lane;
    [SerializeField] HitLinePulse hitPulse;
    [SerializeField] JudgementPopup popup;

    void OnEnable(){ if (lane) lane.OnJudged += OnJudge; }
    void OnDisable(){ if (lane) lane.OnJudged -= OnJudge; }
    void OnJudge(Judgement j){ hitPulse?.Ping(); popup?.Show(j); }
}