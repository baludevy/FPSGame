using UnityEngine;

public static class TimeScaler {
    private static float sensitivity = 0.02f;
    private static float smoothing = 0.4f;
    private static float jitterDeadbandMult = 0.5f;
    private static float maxOffset = 0.05f;

    private static float currentMargin;
    private static float targetTimescale = 1f;
    private static float currentDeadband;
    private static float stableDuration;
    private static float lastSampleTime;

    public static void AdjustInputClock(float marginSeconds) {
        currentMargin = marginSeconds;

        float now = FixedClock.GetTime();
        float deltaTime = now - lastSampleTime;
        lastSampleTime = now;

        float deviation = marginSeconds - NetworkSettings.targetInputMargin;
        
        currentDeadband = Mathf.Clamp(NetStatistics.upstreamJitter * jitterDeadbandMult, 0.002f, 0.05f);

        if (Mathf.Abs(deviation) <= currentDeadband) {
            stableDuration += deltaTime;
            targetTimescale = 1f;
        } else {
            stableDuration = 0f;
            float pTerm = -(deviation / NetworkSettings.tickTime) * sensitivity;
            targetTimescale = Mathf.Clamp(1f + pTerm, 1f - maxOffset, 1f + maxOffset);
        }

        FixedClock.timeScale = Mathf.Lerp(FixedClock.timeScale, targetTimescale, smoothing);
    }

    public static float GetCurrentDeadband() {
        return currentDeadband;
    }

    public static float GetStableDuration() {
        return stableDuration;
    }

    public static float GetCurrentMargin() {
        return currentMargin;
    }

    public static void Reset() {
        currentMargin = 0f;
        targetTimescale = 1f;
        currentDeadband = 0f;
        stableDuration = 0f;
        lastSampleTime = 0f;
        FixedClock.timeScale = 1f;
    }
}