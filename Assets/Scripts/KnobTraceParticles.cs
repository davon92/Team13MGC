using UnityEngine;

public class KnobTraceParticles : MonoBehaviour
{
    public enum Mode { FollowLoop, BeatBursts }

    [Header("Wiring")]
    [SerializeField] KnobLaneController lane;
    [SerializeField] RectTransform     playerDot;   // the stick icon
    [SerializeField] RhythmConductor   conductor;

    [Header("FollowLoop")]
    [SerializeField] ParticleSystem loopPs;         // already in scene, set to Loop, Emission disabled in inspector
    [SerializeField] bool alignToMotion = false;

    [Header("BeatBursts")]
    [SerializeField] ParticleSystem burstPrefab;    // optional pooled prefab
    [SerializeField] Transform      spawnParent;    // usually the stick
    [SerializeField, Min(0.05f)] float burstEveryBeats = 0.25f;
    [SerializeField, Range(1, 40)] int burstCount = 8;

    [Header("General")]
    [SerializeField] Mode mode = Mode.FollowLoop;
    [SerializeField] float rateWhenTracing = 60f;   // if you switch loopPs to non-looping and want rate bursts

    float _nextBurstBeat;

    void OnEnable()
    {
        if (loopPs) loopPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _nextBurstBeat = 0f;
    }

    void Update()
    {
        if (!lane || !playerDot) return;

        bool onTarget = lane.TryGetVisualError(out var err) && err <= lane.GoodWindow;

        // Always follow the stick
        if (loopPs)
        {
            loopPs.transform.position = playerDot.position;

            if (alignToMotion)
            {
                // Optional: face movement direction (simple velocity estimate)
                var v = (Vector2)playerDot.TransformVector(Vector2.up); // "up" of the icon
                if (v.sqrMagnitude > 0.0001f)
                    loopPs.transform.rotation = Quaternion.LookRotation(Vector3.forward, v.normalized);
            }
        }

        if (mode == Mode.FollowLoop)
        {
            if (!loopPs) return;

            var em = loopPs.emission;
            em.enabled = onTarget;

            if (onTarget)
            {
                if (!loopPs.isPlaying) loopPs.Play();
                // Optional rate control (kept for flexibility)
                var r = em.rateOverTime;
                r.constant = rateWhenTracing;
                em.rateOverTime = r;
            }
            else
            {
                if (loopPs.isPlaying) loopPs.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }
        else // BeatBursts
        {
            if (!burstPrefab || !conductor) return;

            float beat = conductor.NowForHit;
            if (!onTarget)
            {
                _nextBurstBeat = beat; // rearm to "now" so we don't queue up bursts while off path
                return;
            }

            if (beat >= _nextBurstBeat)
            {
                _nextBurstBeat = beat + burstEveryBeats;

                var ps = Instantiate(burstPrefab, playerDot.position, Quaternion.identity,
                                     spawnParent ? spawnParent : transform);
                var main = ps.main;
                main.stopAction = ParticleSystemStopAction.Destroy;
                ps.Emit(burstCount);
            }
        }
    }
}
