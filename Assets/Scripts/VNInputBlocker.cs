using UnityEngine;

/// Simple global gate you can flip for a short time to ignore VN advance.
public static class VNInputBlocker
{
    static float _blockUntilUnscaledTime = 0f;

    /// Block VN advances for `seconds` (unscaled time).
    public static void BlockAdvance(float seconds = 0.12f)
    {
        float until = Time.unscaledTime + Mathf.Max(0f, seconds);
        if (until > _blockUntilUnscaledTime) _blockUntilUnscaledTime = until;
    }

    public static bool ShouldBlock => Time.unscaledTime < _blockUntilUnscaledTime;
}