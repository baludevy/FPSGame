using System;
using UnityEngine;

public static class NetStatisticsManager {
    private static float pingSmooth = 0.1f;
    private static float packetLossSmooth = 0.1f;

    private static float lastClientReceive = -1f;
    private static float lastServerSend = -1f;

    private static float inputMarginSmooth = NetworkSettings.tickTime / 0.25f;

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

        NetStatistics.inputMargin = (inputMarginSmooth * timing.inputReceiveMargin) +
                                    ((1f - inputMarginSmooth) * lastInputMargin);

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

    public static void Reset() {
        NetStatistics.ping = NetStatistics.downstreamPacketLoss =
            NetStatistics.downstreamJitter = NetStatistics.upstreamJitter = 0f;
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