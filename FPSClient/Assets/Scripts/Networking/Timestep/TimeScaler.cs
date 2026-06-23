using UnityEngine;

public static class TimeScaler {
    private static float normalTimescale = 1f;
    private static float minTimescale = 0.98f;
    private static float maxTimescale = 1.02f;

    private static float sensitivity = 0.02f;
    private static float smoothing = 0.25f;

    private static float currentMargin;
    private static float targetTimescale = 1f;
    private static float currentThreshold;

    private static float stableDuration;
    private static float lastSampleTime;
    private static bool hasSample;

    public static void AdjustInputClock(float marginSeconds) {
        float tickTime = NetworkSettings.tickTime;
        float targetSeconds = NetworkSettings.targetServerMargin;

        currentMargin = marginSeconds;

        float now = FixedClock.GetTime();
        float deltaTime = hasSample ? Mathf.Max(now - lastSampleTime, 0.0001f) : 0f;

        lastSampleTime = now;
        hasSample = true;

        currentThreshold = Mathf.Min(targetSeconds * 0.25f, tickTime * 0.5f);

        float deviation = marginSeconds - targetSeconds;
        bool withinThreshold = Mathf.Abs(deviation) <= currentThreshold;

        if (withinThreshold) {
            stableDuration += deltaTime;
            targetTimescale = normalTimescale;
        }
        else {
            stableDuration = 0f;

            float adjustment = -(deviation / tickTime) * sensitivity;

            targetTimescale = Mathf.Clamp(
                normalTimescale + adjustment,
                minTimescale,
                maxTimescale);
        }

        FixedClock.timeScale = Mathf.Lerp(
            FixedClock.timeScale,
            targetTimescale,
            smoothing);
    }

    public static float GetCurrentMargin() {
        return currentMargin;
    }

    public static void Reset() {
        currentMargin = 0f;
        targetTimescale = 1f;
        currentThreshold = 0f;

        stableDuration = 0f;
        lastSampleTime = 0f;
        hasSample = false;

        FixedClock.timeScale = 1f;
    }
}