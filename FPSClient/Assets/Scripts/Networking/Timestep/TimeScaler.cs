using UnityEngine;

public static class TimeScaler {
    private static float sensitivity = 0.02f;
    private static float catchUpGainMult = 3f;
    private static float jitterDeadbandMult = 0.5f;

    private static float maxSpeedUp = 0.1f;
    private static float maxSlowDown = 0.03f;

    private static float speedUpSmoothing = 0.6f;
    private static float slowDownSmoothing = 0.15f;
    
    private static float marginFilterTime = 0.0625f;

    private static bool allowSnap = true;
    private static float snapThresholdTicks = 4f;
    private static float maxSnapSeconds = 0.5f;
    private static int stepConfirmSamples = 2;

    private static float currentMargin;
    private static float targetTimescale = 1f;
    private static float currentDeadband;
    private static float lastSampleTime;

    private static float pendingCorrection;
    private static int bigDeviationStreak;

    public static void AdjustInputClock(float marginSeconds) {
        if (TickSync.syncing) return;

        currentMargin = marginSeconds;

        float now = FixedClock.GetTime();
        float deltaTime = now - lastSampleTime;
        lastSampleTime = now;
        if (deltaTime <= 0f || deltaTime > 1f)
            deltaTime = NetworkSettings.tickTime;

        float tau = Mathf.Max(0.02f, NetStatistics.ping + marginFilterTime);
        pendingCorrection *= Mathf.Exp(-deltaTime / tau);

        float deviation = marginSeconds - NetworkSettings.targetInputMargin;
        float effectiveDeviation = deviation + pendingCorrection;

        currentDeadband = Mathf.Clamp(NetStatistics.upstreamJitter * jitterDeadbandMult, 0.002f, 0.05f);

        float snapThreshold = snapThresholdTicks * NetworkSettings.tickTime;
        bool genuinelyBehind = deviation < -snapThreshold && pendingCorrection < snapThreshold;
        bigDeviationStreak = genuinelyBehind ? bigDeviationStreak + 1 : 0;

        if (allowSnap && bigDeviationStreak >= stepConfirmSamples) {
            float snap = Mathf.Min(-deviation - pendingCorrection, maxSnapSeconds);
            if (snap > 0f) {
                FixedClock.Nudge(snap);
                pendingCorrection = Mathf.Clamp(pendingCorrection + snap, 0f, maxSnapSeconds);
            }

            bigDeviationStreak = 0;
            targetTimescale = 1f;
            FixedClock.timeScale = Mathf.Lerp(FixedClock.timeScale, targetTimescale, slowDownSmoothing);
            return;
        }

        if (Mathf.Abs(effectiveDeviation) <= currentDeadband) {
            targetTimescale = 1f;
        }
        else {
            float gain = sensitivity * (effectiveDeviation < 0f ? catchUpGainMult : 1f);
            float pTerm = -(effectiveDeviation / NetworkSettings.tickTime) * gain;
            targetTimescale = Mathf.Clamp(1f + pTerm, 1f - maxSlowDown, 1f + maxSpeedUp);
        }

        float smoothing = (targetTimescale > FixedClock.timeScale) ? speedUpSmoothing : slowDownSmoothing;
        float newScale = Mathf.Lerp(FixedClock.timeScale, targetTimescale, smoothing);

        pendingCorrection += Mathf.Max(0f, newScale - 1f) * deltaTime;
        pendingCorrection = Mathf.Clamp(pendingCorrection, 0f, maxSnapSeconds);

        FixedClock.timeScale = newScale;
    }
    

    public static float GetCurrentMargin() {
        return currentMargin;
    }

    public static void Reset() {
        currentMargin = 0f;
        targetTimescale = 1f;
        currentDeadband = 0f;
        lastSampleTime = 0f;
        pendingCorrection = 0f;
        bigDeviationStreak = 0;
        FixedClock.timeScale = 1f;
    }
}