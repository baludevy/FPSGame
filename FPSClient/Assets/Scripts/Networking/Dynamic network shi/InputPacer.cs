using UnityEngine;

public static class InputPacer {
    private static float targetMargin => NetcodeState.targetInputMargin;

    // pacer settings
    private static float sensitivity = 0.02f;
    private static float catchUpGainMult = 3f;
    private static float jitterDeadbandMult = 0.5f;

    // timescale bounds
    private static float maxSpeedUp = 0.4f;
    private static float maxSlowDown = 0.2f;

    // timescale smoothing
    private static float speedUpSmoothing = 0.6f;
    private static float slowDownSmoothing = 0.3f;

    // hard snap settings
    private static bool allowSnap = true;
    private static float snapThreshold = 0.25f;
    private static float maxSnapSeconds = 0.5f;
    private static int stepConfirmSamples = 2;

    // state variables
    private static float currentMargin;
    private static float targetTimescale = 1f;
    private static float lastSampleTime;

    private static float pendingCorrection;

    // streak counters
    private static int behindStreak;

    public static void AdjustInputClock(float inputMargin) {
        currentMargin = inputMargin;

        float now = FixedClock.GetTime();
        float deltaTime = now - lastSampleTime;
        lastSampleTime = now;
        if (deltaTime <= 0f || deltaTime > 1f) {
            deltaTime = NetworkSettings.tickTime;
        }

        float tau = Mathf.Max(0.02f, NetStatistics.ping);
        pendingCorrection *= Mathf.Exp(-deltaTime / tau);

        float deviation = inputMargin - targetMargin;
        float effectiveDeviation = deviation + pendingCorrection;

        float currentDeadband = Mathf.Clamp(NetStatistics.upstreamJitter * jitterDeadbandMult, 0.002f, 0.05f);

        bool genuinelyBehind = deviation < -snapThreshold && pendingCorrection < snapThreshold;

        if (genuinelyBehind) {
            behindStreak = behindStreak + 1;
        }
        else {
            behindStreak = 0;
        }

        // check if snap conditions are satisfied
        if (allowSnap && behindStreak >= stepConfirmSamples) {
            float wanted = -deviation - pendingCorrection;
            float snap = Mathf.Clamp(wanted, -maxSnapSeconds, maxSnapSeconds);

            if (Mathf.Abs(snap) > 0f) {
                FixedClock.Nudge(snap);
                pendingCorrection = Mathf.Clamp(pendingCorrection + snap, -maxSnapSeconds, maxSnapSeconds);
            }

            behindStreak = 0;
            targetTimescale = 1f;
            FixedClock.timeScale = Mathf.Lerp(FixedClock.timeScale, targetTimescale, slowDownSmoothing);
            return;
        }

        // recalculate target timescale based on deadband
        if (Mathf.Abs(effectiveDeviation) <= currentDeadband) {
            targetTimescale = 1f;
        }
        else {
            float gain;
            if (effectiveDeviation < 0f) {
                gain = sensitivity * catchUpGainMult;
            }
            else {
                gain = sensitivity * 1f;
            }
            float pTerm = -(effectiveDeviation / NetworkSettings.tickTime) * gain;
            targetTimescale = Mathf.Clamp(1f + pTerm, 1f - maxSlowDown, 1f + maxSpeedUp);
        }

        float smoothing;
        if (targetTimescale > FixedClock.timeScale) {
            smoothing = speedUpSmoothing;
        }
        else {
            smoothing = slowDownSmoothing;
        }
        float newScale = Mathf.Lerp(FixedClock.timeScale, targetTimescale, smoothing);

        pendingCorrection += (newScale - 1f) * deltaTime;
        pendingCorrection = Mathf.Clamp(pendingCorrection, -maxSnapSeconds, maxSnapSeconds);

        FixedClock.timeScale = newScale;
    }

    public static float GetCurrentInputMargin() {
        return currentMargin;
    }

    public static void Reset() {
        currentMargin = 0f;
        targetTimescale = 1f;
        lastSampleTime = 0f;
        pendingCorrection = 0f;
        behindStreak = 0;
        FixedClock.timeScale = 1f;
    }
}