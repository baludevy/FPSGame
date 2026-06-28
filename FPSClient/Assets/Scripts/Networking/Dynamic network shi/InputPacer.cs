using UnityEngine;

public static class InputPacer {
    private static float targetMargin => NetcodeState.targetInputMargin;

    // pacer settings
    private static float sensitivity = 0.02f;
    private static float catchUpGainMult = 3f;
    private static float jitterDeadbandMult = 0.1f;
    private static float frametimeDeadbandMult = 0.1f;

    // timescale bounds
    private static float maxSpeedUp = 0.1f;
    private static float maxSlowDown = 0.05f;

    // timescale smoothing
    private static float speedUpSmoothing = 0.6f;
    private static float slowDownSmoothing = 0.3f;

    // state variables
    private static float currentMargin;
    private static float targetTimescale = 1f;
    private static float lastSampleTime;

    private static float pendingCorrection;

    public static void AdjustInputClock(float inputMargin, uint serverTick) {
        currentMargin = inputMargin;

        if (currentMargin > 0.23f || currentMargin < -0.23f) {
            Debug.Log((uint)TickUtil.SecondsToTick(NetStatistics.ping + NetcodeState.targetInputMargin));

            FixedClock.tick =
                serverTick + (uint)TickUtil.SecondsToTick(NetStatistics.ping + NetcodeState.targetInputMargin);

            targetTimescale = 1f;
            FixedClock.timeScale = 1f;
            pendingCorrection = 0f;

            return;
        }

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

        float jitterDeadband = NetStatistics.upstreamJitter * jitterDeadbandMult;
        float frametimeDeadband = (FrametimeMonitor.meanFrametime + 2f * FrametimeMonitor.frametimeStdDev) *
            frametimeDeadbandMult;
        
        float currentDeadband = Mathf.Clamp(jitterDeadband + frametimeDeadband, 0.002f, 0.05f);

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
        pendingCorrection = Mathf.Clamp(pendingCorrection, -0.5f, 0.5f);

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
        FixedClock.timeScale = 1f;
    }
}