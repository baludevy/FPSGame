using System;
using UnityEngine;

public static class NetStatisticsManager {
    private static float pingSmooth = 0.1f;
    private static float packetLossSmooth = 0.1f;

    private static float lastClientReceive = -1f;
    private static float lastClientSend = -1f;
    private static float lastServerSend = -1f;
    private static float lastServerReceive = -1f;

    private static int jitterWindow = 128;
    private static readonly float[] jitterSamples = new float[jitterWindow];
    private static readonly float[] jitterSorted = new float[jitterWindow];
    private static int jitterIndex;
    private static int jitterCount;

    private static int lossWindow = 128;
    private static readonly uint[] receivedTicks = new uint[lossWindow];
    private static readonly bool[] receivedMask = new bool[lossWindow];
    private static int receivedCount;
    private static uint latestTick;
    private static bool hasTick;

    private static float lastInputMargin;

    public static void UpdateStatistics(uint serverTick, float clientReceive, TimingInfo timing,
        UpstreamStatistics upstream) {
        float serverProcessTime = timing.serverSendTime - timing.serverReceiveTime;
        float pingSample = clientReceive - timing.clientSendTimeAck - serverProcessTime;

        NetStatistics.ping = Mathf.Max(0f, Mathf.Lerp(NetStatistics.ping, pingSample, pingSmooth));

        NetStatistics.inputMargin = (0.1f * timing.inputReceiveMargin) + ((1f - 0.1f) * lastInputMargin);

        UpdateJitter(timing.serverSendTime, clientReceive);
        UpdatePacketLoss(serverTick);

        NetStatistics.upstreamJitter = upstream.jitter;
        NetStatistics.upstreamPacketLoss = upstream.packetLoss;

        lastInputMargin = NetStatistics.inputMargin;
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

        jitterSamples[jitterIndex] = delta;
        jitterIndex = (jitterIndex + 1) % jitterWindow;
        if (jitterCount < jitterWindow) {
            jitterCount++;
        }

        Array.Copy(jitterSamples, jitterSorted, jitterCount);
        Array.Sort(jitterSorted, 0, jitterCount);

        int p95Index = Mathf.Clamp(Mathf.CeilToInt(jitterCount * 0.95f) - 1, 0, jitterCount - 1);
        NetStatistics.downstreamJitter = jitterSorted[p95Index];
    }

    private static void UpdatePacketLoss(uint serverTick) {
        if (!hasTick) {
            hasTick = true;
            latestTick = serverTick;

            int slot = (int)(serverTick % lossWindow);
            receivedMask[slot] = true;
            receivedTicks[slot] = serverTick;
            receivedCount = 1;
            return;
        }

        int tickDelta = (int)(serverTick - latestTick);

        if (tickDelta > 0) {
            int ticksToClear = Mathf.Min(tickDelta, lossWindow);
            for (int i = 1; i < ticksToClear; i++) {
                uint clearTick = latestTick + (uint)i;
                int clearSlot = (int)(clearTick % lossWindow);

                if (receivedMask[clearSlot] && receivedTicks[clearSlot] == clearTick - (uint)lossWindow) {
                    receivedMask[clearSlot] = false;
                    receivedCount--;
                }
            }

            latestTick = serverTick;
        }

        int arrivedSlot = (int)(serverTick % lossWindow);

        if (tickDelta >= -lossWindow) {
            if (!receivedMask[arrivedSlot] || receivedTicks[arrivedSlot] != serverTick) {
                if (receivedMask[arrivedSlot]) {
                    receivedCount--;
                }

                receivedMask[arrivedSlot] = true;
                receivedTicks[arrivedSlot] = serverTick;
                receivedCount++;
            }
        }

        float sampleLoss = 1f - (Mathf.Clamp(receivedCount, 0, lossWindow) / (float)lossWindow);
        NetStatistics.downstreamPacketLoss =
            Mathf.Lerp(NetStatistics.downstreamPacketLoss, sampleLoss, packetLossSmooth);
    }

    public static void ApplyAdjustments() {
        float jitterInTicks = NetStatistics.downstreamJitter / NetworkSettings.tickTime;
        int calculatedBuffer = Mathf.CeilToInt(jitterInTicks) + 1;
        NetworkSettings.interpTime = Mathf.Max(2, calculatedBuffer) * NetworkSettings.tickTime;

        float baseBuffer = 0.005f;
        float jitterPad = NetStatistics.upstreamJitter * 0.95f;
        float packetLossPad = Mathf.Clamp((NetStatistics.upstreamPacketLoss / 10f), 0f, 0.04f);

        Debug.Log(packetLossPad);

        float targetNow =
            Mathf.Clamp(baseBuffer + jitterPad + packetLossPad, baseBuffer, NetworkSettings.tickTime * 4f);
        NetworkSettings.targetInputMargin = Mathf.Lerp(NetworkSettings.targetInputMargin, targetNow, 0.1f);
    }

    public static void Reset() {
        NetStatistics.ping = NetStatistics.downstreamPacketLoss =
            NetStatistics.downstreamJitter = NetStatistics.upstreamJitter = 0f;
        lastClientReceive = lastServerSend = -1f;
        jitterIndex = jitterCount = receivedCount = 0;
        latestTick = 0;
        hasTick = false;

        Array.Clear(jitterSamples, 0, jitterSamples.Length);
        Array.Clear(jitterSorted, 0, jitterSorted.Length);
        Array.Clear(receivedMask, 0, receivedMask.Length);
        Array.Clear(receivedTicks, 0, receivedTicks.Length);
    }
}

public static class NetStatistics {
    public static float ping;
    public static float upstreamJitter;
    public static float downstreamJitter;
    public static float upstreamPacketLoss;
    public static float downstreamPacketLoss;
    public static float bytesSent;
    public static float bytesReceived;
    public static int packetsSent;
    public static int packetsReceived;
    public static float inputMargin;
}