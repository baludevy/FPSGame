using System;
using UnityEngine;

public static class ConnectionStatistics {
    public static float ping;
    public static float packetLoss;
    public static float jitter;
    public static float inputJitter;
    public static float totalRtt;

    private static float pingSmooth = 0.1f;
    private static float packetLossSmooth = 0.1f;

    private static float lastClientReceive = -1f;
    private static float lastServerSend = -1f;

    private static int jitterWindow = 128;
    private static float[] jitterSamples = new float[jitterWindow];
    private static int jitterIndex;
    private static int jitterCount;
    private static float[] jitterSorted = new float[jitterWindow];

    private static uint lossWindow = 128;
    private static bool[] received = new bool[lossWindow];
    private static int receivedCount;
    private static uint latestTick;
    private static bool hasTick;

    public static void UpdateStatistics(
        uint serverTick,
        float clientReceive,
        float clientSend,
        float serverReceive,
        float serverSend,
        float serverInputJitter) {
        float serverProcessTime = serverSend - serverReceive;

        float pingSample = clientReceive - clientSend - serverProcessTime;
        float totalRttSample = clientReceive - clientSend + NetworkSettings.interpTime;

        inputJitter = serverInputJitter;

        UpdatePing(pingSample);
        UpdateTotalRtt(totalRttSample);
        UpdateJitter(serverSend, clientReceive);
        UpdatePacketLoss(serverTick);
    }

    private static void UpdatePing(float pingSample) {
        if (ping == 0f) {
            ping = pingSample;
        }

        ping = Mathf.Max(0f, (pingSample * pingSmooth) + (ping * (1f - pingSmooth)));
    }

    private static void UpdateTotalRtt(float totalRttSample) {
        if (totalRtt == 0f) {
            totalRtt = totalRttSample;
        }

        totalRtt = Mathf.Max(0f, (totalRttSample * pingSmooth) + (totalRtt * (1f - pingSmooth)));
    }

    private static void UpdateJitter(float serverSend, float clientReceive) {
        if (lastClientReceive < 0f) {
            lastClientReceive = clientReceive;
            lastServerSend = serverSend;
            return;
        }

        float arrivalDelta = clientReceive - lastClientReceive;
        float sendDelta = serverSend - lastServerSend;

        float delta = Mathf.Abs(arrivalDelta - sendDelta);

        lastClientReceive = clientReceive;
        lastServerSend = serverSend;

        float oldSample = jitterSamples[jitterIndex];
        jitterSamples[jitterIndex] = delta;
        jitterIndex = (jitterIndex + 1) % jitterWindow;

        if (jitterCount < jitterWindow) {
            jitterCount++;

            int insertIndex = Array.BinarySearch(jitterSorted, 0, jitterCount - 1, delta);
            if (insertIndex < 0) {
                insertIndex = ~insertIndex;
            }

            Array.Copy(
                jitterSorted,
                insertIndex,
                jitterSorted,
                insertIndex + 1,
                (jitterCount - 1) - insertIndex);

            jitterSorted[insertIndex] = delta;
        }
        else {
            int removeIndex = Array.BinarySearch(jitterSorted, 0, jitterWindow, oldSample);

            if (removeIndex >= 0) {
                Array.Copy(
                    jitterSorted,
                    removeIndex + 1,
                    jitterSorted,
                    removeIndex,
                    jitterWindow - removeIndex - 1);
            }

            int insertIndex = Array.BinarySearch(jitterSorted, 0, jitterWindow - 1, delta);

            if (insertIndex < 0) {
                insertIndex = ~insertIndex;
            }

            Array.Copy(
                jitterSorted,
                insertIndex,
                jitterSorted,
                insertIndex + 1,
                (jitterWindow - 1) - insertIndex);

            jitterSorted[insertIndex] = delta;
        }

        int p95Index = Mathf.CeilToInt(jitterCount * 0.95f) - 1;
        p95Index = Mathf.Clamp(p95Index, 0, jitterCount - 1);

        jitter = jitterSorted[p95Index];
    }

    private static void UpdatePacketLoss(uint serverTick) {
        if (!hasTick) {
            hasTick = true;
            latestTick = serverTick;
        }

        if (serverTick <= latestTick) {
            return;
        }

        uint missedTicks = serverTick - latestTick - 1;

        if (missedTicks > lossWindow) {
            missedTicks = lossWindow;
        }

        for (uint i = 1; i <= missedTicks; i++) {
            int slot = (int)((latestTick + i) % lossWindow);

            if (received[slot]) {
                received[slot] = false;
                receivedCount--;
            }
        }

        int arrivedSlot = (int)(serverTick % lossWindow);

        if (!received[arrivedSlot]) {
            receivedCount++;
        }

        received[arrivedSlot] = true;
        latestTick = serverTick;

        float sampleLoss = 1f - (receivedCount / (float)lossWindow);

        packetLoss = (sampleLoss * packetLossSmooth) +
                     (packetLoss * (1f - packetLossSmooth));
    }

    public static void ApplyAdjustments() {
        float jitterInTicks = jitter / NetworkSettings.tickTime;

        int calculatedBuffer =
            Mathf.CeilToInt(jitterInTicks) + 1;

        float baseBuffer = 0.005f;
        float jitterPad = inputJitter * 0.95f;

        float targetNow = Mathf.Clamp(
            baseBuffer + jitterPad,
            baseBuffer,
            NetworkSettings.tickTime * 4f);

        NetworkSettings.targetServerMargin =
            Mathf.Lerp(NetworkSettings.targetServerMargin, targetNow, 0.1f);

        NetworkSettings.interpTime =
            Math.Max(2, calculatedBuffer) * NetworkSettings.tickTime;

        float lossPercentage = packetLoss * 100f;

        NetworkSettings.inputRedundancy =
            Mathf.Clamp(Mathf.RoundToInt(lossPercentage / 3f), 1, 5);
    }

    public static void Reset() {
        ping = 0f;
        packetLoss = 0f;
        jitter = 0f;
        inputJitter = 0f;
        totalRtt = 0f;

        lastClientReceive = -1f;
        lastServerSend = -1f;

        Array.Clear(jitterSamples, 0, jitterSamples.Length);
        Array.Clear(jitterSorted, 0, jitterSorted.Length);

        jitterIndex = 0;
        jitterCount = 0;

        Array.Clear(received, 0, received.Length);

        receivedCount = 0;
        latestTick = 0;
        hasTick = false;
    }
}