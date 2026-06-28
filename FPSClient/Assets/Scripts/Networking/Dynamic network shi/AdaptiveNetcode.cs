using UnityEngine;

public static class AdaptiveNetcode {
    // input margin settings
    private static float baseInputMargin = 0.005f; // 5ms base
    private static float maxInputMarginTime = 0.2f; // 200ms max (server accepts 250 max)
    private static float inputMarginRiseLerp = 0.2f;
    private static float inputMarginFallLerp = 0.02f;

    // receive margin settings
    private static float baseReceiveMargin = 0.005f; // 3m base
    private static float maxReceiveMargin = 0.1f; // 100ms max
    private static float receiveMarginRiseLerp = 0.15f;
    private static float receiveMarginFallLerp = 0.01f;

    // network and frametime jitter
    private static float jitterMarginMult = 1.2f;
    private static float frametimeMarginMult = 0.25f;
    private static float marginLowerHold = 5f;

    // packet loss
    private static float lossMarginScale = 0.08f;
    private static float maxLossMargin = 0.04f;

    // input redundancy
    private static int currentRedundancy;
    private static float redundancyReleaseHold = 3f;

    //timers
    private static float inputMarginLowerTimer;
    private static float committedInputMarginTarget;

    private static float committedReceiveMarginTarget;
    private static float receiveMarginLowerTimer;


    private static float redundancyLowerTimer;

    private static float lastTime = -1f;
    private static bool initialized;

    public static void Apply() {
        float now = FixedClock.GetTime();
        float deltaTime = Mathf.Max(0f, now - lastTime);
        lastTime = now;

        if (!initialized) {
            currentRedundancy = 0;
            committedInputMarginTarget = baseInputMargin;
            NetcodeState.targetInputMargin = baseInputMargin;
            committedReceiveMarginTarget = baseReceiveMargin;
            NetcodeState.targetReceiveMargin = baseReceiveMargin;

            initialized = true;
        }

        if (deltaTime <= 0f) return;

        UpdateInputMargin(deltaTime);
        UpdateReceiveMargin(deltaTime);
        UpdateRedundancy(deltaTime);
    }

    private static void UpdateInputMargin(float deltaTime) {
        float marginOffset =
            ComputeMarginPadding(baseInputMargin, NetStatistics.upstreamJitter, NetStatistics.upstreamPacketLoss);

        float rawTarget = Mathf.Clamp(
            baseInputMargin + marginOffset,
            baseInputMargin, maxInputMarginTime);

        // immediately increase the current target if the calculated target is higher
        if (rawTarget > committedInputMarginTarget) {
            committedInputMarginTarget = rawTarget;
            inputMarginLowerTimer = 0f;
        }
        // only drop current target after some time has passed
        else if (rawTarget < committedInputMarginTarget) {
            inputMarginLowerTimer += deltaTime;
            if (inputMarginLowerTimer >= marginLowerHold) {
                committedInputMarginTarget = rawTarget;
                inputMarginLowerTimer = 0f;
            }
        }
        else {
            inputMarginLowerTimer = 0f;
        }

        float lerpRate = committedInputMarginTarget > NetcodeState.targetInputMargin
            ? inputMarginRiseLerp
            : inputMarginFallLerp;

        float blend = 1f - Mathf.Exp(-lerpRate * deltaTime * NetworkSettings.tickRate);

        NetcodeState.targetInputMargin =
            Mathf.Lerp(NetcodeState.targetInputMargin, committedInputMarginTarget, blend);
    }

    private static void UpdateReceiveMargin(float deltaTime) {
        if (UpdateManager.Instance == null) return;

        float marginOffset = ComputeMarginPadding(baseReceiveMargin, NetStatistics.downstreamJitter,
            NetStatistics.downstreamPacketLoss);

        float rawTarget = Mathf.Clamp(baseReceiveMargin + marginOffset, baseReceiveMargin, maxReceiveMargin);

        // immediately increase the current target if the calculated target is higher
        if (rawTarget > committedReceiveMarginTarget) {
            committedReceiveMarginTarget = rawTarget;
            receiveMarginLowerTimer = 0f;
        }
        // only drop current target after some time has passed
        else if (rawTarget < committedReceiveMarginTarget) {
            receiveMarginLowerTimer += deltaTime;
            if (receiveMarginLowerTimer >= marginLowerHold) {
                committedReceiveMarginTarget = rawTarget;
                receiveMarginLowerTimer = 0f;
            }
        }
        else {
            receiveMarginLowerTimer = 0f;
        }

        float lerpRate = committedReceiveMarginTarget > NetcodeState.targetReceiveMargin
            ? receiveMarginRiseLerp
            : receiveMarginFallLerp;

        float blend = 1f - Mathf.Exp(-lerpRate * deltaTime * NetworkSettings.tickRate);

        NetcodeState.targetReceiveMargin =
            Mathf.Lerp(NetcodeState.targetReceiveMargin, committedReceiveMarginTarget, blend);
    }

    private static void UpdateRedundancy(float deltaTime) {
        int desired = GetDesiredRedundancy(NetStatistics.upstreamPacketLoss);

        if (desired >= currentRedundancy) {
            currentRedundancy = desired;
            redundancyLowerTimer = 0f;
        }
        else {
            redundancyLowerTimer += deltaTime;
            if (redundancyLowerTimer >= redundancyReleaseHold) {
                currentRedundancy = desired;
                redundancyLowerTimer = 0f;
            }
        }

        NetcodeState.inputRedundancy = currentRedundancy;
    }

    private static int GetDesiredRedundancy(float loss) {
        if (loss > 0.10f) return 4;
        if (loss > 0.05f) return 3;
        return 2;
    }

    private static float ComputeMarginPadding(float baseMargin, float jitter, float packetLoss) {
        float jitterPad = jitter * jitterMarginMult;
        float frametimePad = (FrametimeMonitor.meanFrametime + 2f * FrametimeMonitor.frametimeStdDev) *
                             frametimeMarginMult;
        float lossPad = Mathf.Clamp(packetLoss * lossMarginScale, 0f, maxLossMargin);

        float totalPads = jitterPad + frametimePad + lossPad;

        // subtract the base margin so we dont add additional redundant latency if our base margin can absorb the current conditions
        return Mathf.Max(0f, totalPads - baseMargin);
    }

    public static void Reset() {
        currentRedundancy = 0;
        redundancyLowerTimer = 0f;
        inputMarginLowerTimer = 0f;
        receiveMarginLowerTimer = 0f;
        lastTime = -1f;
        initialized = false;
    }
}