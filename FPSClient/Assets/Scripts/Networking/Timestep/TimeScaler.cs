using UnityEngine;

public static class TimeScaler {
    private static int TargetInpBufferSize => NetworkSettings.targetInpBufferSize;

    private static float normalTimescale = 1f;
    private static float minTimescale = 0.95f;
    private static float maxTimescale = 1.2f;

    private static float sensitivity = 0.03f;
    private static float smoothing = 0.25f;

    private static float baseThreshold = 1f;
    private static float minThreshold = 0.3f;
    private static float jitterInfluence = 2f;
    private static float jitterDecay = 1.5f;
    private static float stableLockTime = 2f;

    private static int currentBufferSize;
    private static float targetTimescale = 1f;
    private static float currentThreshold;

    private static float jitter;
    private static float stableDuration;
    private static int lastBufferOffset;
    private static float lastSampleTime;
    private static bool hasSample;

    public static void AdjustClock(int bufferSize) {
        currentBufferSize = bufferSize;

        float now = FixedClock.GetTime();
        float deltaTime = hasSample ? Mathf.Max(now - lastSampleTime, 0.0001f) : 0f;

        if (hasSample) {
            float instantJitter = Mathf.Abs(bufferSize - lastBufferOffset);
            float k = 1f - Mathf.Exp(-jitterDecay * deltaTime);
            jitter = Mathf.Lerp(jitter, instantJitter, k);
        }

        lastBufferOffset = bufferSize;
        lastSampleTime = now;
        hasSample = true;


        float lockIn = Mathf.Clamp01(stableDuration / stableLockTime);
        float floor = Mathf.Lerp(baseThreshold, minThreshold, lockIn);

        currentThreshold = floor + jitter * jitterInfluence;

        int deviation = bufferSize - TargetInpBufferSize;
        bool withinThreshold = Mathf.Abs(deviation) <= currentThreshold;

        if (withinThreshold) stableDuration += deltaTime;
        else stableDuration = 0f;

        if (withinThreshold) {
            targetTimescale = normalTimescale;
        }
        else {
            float adjustment = -deviation * sensitivity;
            targetTimescale = Mathf.Clamp(normalTimescale + adjustment, minTimescale, maxTimescale);
        }

        FixedClock.timeScale = Mathf.Lerp(FixedClock.timeScale, targetTimescale, smoothing);
    }

    public static int GetBufferOffset() {
        return currentBufferSize;
    }
}