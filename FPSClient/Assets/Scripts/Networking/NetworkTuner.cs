using System;
using UnityEngine;

public static class NetworkTuner {
    private static float baseInputMargin = 0.003f;
    private static float inputMarginRiseLerp = 0.2f;
    private static float inputMarginFallLerp = 0.02f;
    private static float maxInputMarginTime = 0.050f;

    private static float baseReceiveMargin = 0.003f;
    private static float maxReceiveMargin = 0.090f;
    private static float receiveMarginRiseLerp = 0.15f;
    private static float receiveMarginFallLerp = 0.01f;
    private static float committedReceiveMarginTarget;
    private static float receiveMarginLowerTimer;

    private static float jitterMarginMult = 1.2f;
    private static float frametimeMarginMult = 0.3f;
    private static float lossMarginScale = 0.08f;
    private static float maxLossMargin = 0.04f;
    private static float marginLowerHold = 3f;
    private static float marginLowerHysteresis = 0.002f;

    private static float redundancyReleaseHold = 2f;

    private static int currentRedundancy;
    private static float redundancyLowerTimer;

    private static float marginLowerTimer;
    private static float committedMarginTarget;

    private static float lastTime = -1f;
    private static bool initialized;

    public static void Apply() {
        float now = FixedClock.GetTime();
        float dt = lastTime < 0f ? 0f : Mathf.Max(0f, now - lastTime);
        lastTime = now;

        if (!initialized) {
            currentRedundancy = 0;
            committedMarginTarget = baseInputMargin;
            NetworkSettings.targetInputMargin = baseInputMargin;
            committedReceiveMarginTarget = baseReceiveMargin;
            if (SnapshotManager.Instance != null) {
                SnapshotManager.Instance.targetMargin = baseReceiveMargin;
            }

            initialized = true;
        }

        if (dt <= 0f) return;

        UpdateInputMargin(dt);
        UpdateReceiveMargin(dt);
        UpdateRedundancy(dt);
    }

    private static void UpdateInputMargin(float dt) {
        float jitterPad = NetStatistics.upstreamJitter * jitterMarginMult;
        float frametimePad = (FrametimeMonitor.meanFrametime + 2f * FrametimeMonitor.frametimeStdDev) *
                             frametimeMarginMult;
        float lossPad = Mathf.Clamp(NetStatistics.upstreamPacketLoss * lossMarginScale, 0f, maxLossMargin);

        float totalPads = jitterPad + frametimePad + lossPad;
        float netAdditionalMargin = Mathf.Max(0f, totalPads - baseInputMargin);

        float rawTarget = Mathf.Clamp(
            baseInputMargin + netAdditionalMargin,
            baseInputMargin, maxInputMarginTime);

        if (rawTarget > committedMarginTarget) {
            committedMarginTarget = rawTarget;
            marginLowerTimer = 0f;
        }
        else if (rawTarget < committedMarginTarget - marginLowerHysteresis) {
            marginLowerTimer += dt;
            if (marginLowerTimer >= marginLowerHold) {
                committedMarginTarget = rawTarget;
                marginLowerTimer = 0f;
            }
        }
        else {
            marginLowerTimer = 0f;
        }

        float lerpRate = committedMarginTarget > NetworkSettings.targetInputMargin
            ? inputMarginRiseLerp
            : inputMarginFallLerp;
        float blend = 1f - Mathf.Exp(-lerpRate * dt * 64f);
        NetworkSettings.targetInputMargin = Mathf.Lerp(NetworkSettings.targetInputMargin, committedMarginTarget, blend);
    }

    private static void UpdateReceiveMargin(float dt) {
        if (SnapshotManager.Instance == null) return;

        float jitterPad = NetStatistics.downstreamJitter * jitterMarginMult;
        float frametimePad = (FrametimeMonitor.meanFrametime + 2f * FrametimeMonitor.frametimeStdDev) *
                             frametimeMarginMult;
        float lossPad = Mathf.Clamp(NetStatistics.downstreamPacketLoss * lossMarginScale, 0f, maxLossMargin);
        
        float totalPads = jitterPad + frametimePad + lossPad;
        float netAdditionalMargin = Mathf.Max(0f, totalPads - baseReceiveMargin);

        float rawTarget = Mathf.Clamp(
            baseReceiveMargin + netAdditionalMargin,
            baseReceiveMargin, maxReceiveMargin);

        if (rawTarget > committedReceiveMarginTarget) {
            committedReceiveMarginTarget = rawTarget;
            receiveMarginLowerTimer = 0f;
        }
        else if (rawTarget < committedReceiveMarginTarget - marginLowerHysteresis) {
            receiveMarginLowerTimer += dt;
            if (receiveMarginLowerTimer >= marginLowerHold) {
                committedReceiveMarginTarget = rawTarget;
                receiveMarginLowerTimer = 0f;
            }
        }
        else {
            receiveMarginLowerTimer = 0f;
        }

        float lerpRate = committedReceiveMarginTarget > SnapshotManager.Instance.targetMargin
            ? receiveMarginRiseLerp
            : receiveMarginFallLerp;
        float blend = 1f - Mathf.Exp(-lerpRate * dt * 64f);
        SnapshotManager.Instance.targetMargin =
            Mathf.Lerp(SnapshotManager.Instance.targetMargin, committedReceiveMarginTarget, blend);
    }

    private static void UpdateRedundancy(float dt) {
        int desired = DesiredRedundancy(NetStatistics.upstreamPacketLoss);

        if (desired >= currentRedundancy) {
            currentRedundancy = desired;
            redundancyLowerTimer = 0f;
        }
        else {
            redundancyLowerTimer += dt;
            if (redundancyLowerTimer >= redundancyReleaseHold) {
                currentRedundancy = desired;
                redundancyLowerTimer = 0f;
            }
        }

        NetworkSettings.inputRedundancy = currentRedundancy;
    }

    private static int DesiredRedundancy(float loss) {
        if (loss > 0.10f) return 4;
        if (loss > 0.05f) return 3;
        if (loss > 0.01f) return 2;
        return 0;
    }

    public static void Reset() {
        currentRedundancy = 0;
        redundancyLowerTimer = 0f;
        marginLowerTimer = 0f;
        receiveMarginLowerTimer = 0f;
        lastTime = -1f;
        initialized = false;
    }
}