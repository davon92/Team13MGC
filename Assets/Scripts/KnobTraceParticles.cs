using UnityEngine;

public class KnobTraceParticles : MonoBehaviour
{
    [SerializeField] KnobLaneController lane;
    [SerializeField] RectTransform playerDot; // follows the stick marker
    [SerializeField] ParticleSystem ps;
    [SerializeField] float rateWhenTracing = 60f;

    void Update()
    {
        if (!lane || !ps) return;

        // Follow the player dot
        if (playerDot) ps.transform.position = playerDot.transform.position;

        // Emit only while inside the good window
        bool tracing = lane.TryGetVisualError(out var err) && err <= lane.GoodWindow;

        var em = ps.emission;
        em.enabled = true;
        em.rateOverTime = tracing ? rateWhenTracing : 0f;
    }
}

