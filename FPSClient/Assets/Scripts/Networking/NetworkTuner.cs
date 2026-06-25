using UnityEngine;

public static class NetworkTuner {
    private static float baseInputMargin = 0.005f;
    private static float jitterMarginMult = 1f;
    private static float lossMarginScale = 0.08f;
    private static float maxLossMargin = 0.04f;
    private static float marginRiseLerp = 0.2f;
    private static float marginFallLerp = 0.02f;
    private static float maxInputMarginTicks = 4f;
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
            initialized = true;
        }
        
        UpdateInputMargin(dt);
        UpdateRedundancy(dt);
    }

    private static void UpdateInputMargin(float dt) {
        float jitterPad = NetStatistics.upstreamJitter * jitterMarginMult;
        float lossPad = Mathf.Clamp(NetStatistics.upstreamPacketLoss * lossMarginScale, 0f, maxLossMargin);
        float rawTarget = Mathf.Clamp(
            baseInputMargin + jitterPad + lossPad,
            baseInputMargin, NetworkSettings.tickTime * maxInputMarginTicks);

        if (rawTarget >= committedMarginTarget - marginLowerHysteresis) {
            committedMarginTarget = rawTarget;
            marginLowerTimer = 0f;
        }
        else {
            marginLowerTimer += dt;
            if (marginLowerTimer >= marginLowerHold) {
                committedMarginTarget = rawTarget;
                marginLowerTimer = 0f;
            }
        }

        float lerp = committedMarginTarget > NetworkSettings.targetInputMargin ? marginRiseLerp : marginFallLerp;
        NetworkSettings.targetInputMargin = Mathf.Lerp(NetworkSettings.targetInputMargin, committedMarginTarget, lerp);
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
        committedMarginTarget = baseInputMargin;
        lastTime = -1f;
        initialized = false;
    }
}