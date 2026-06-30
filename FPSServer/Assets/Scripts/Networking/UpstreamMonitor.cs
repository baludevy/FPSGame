using System;
using UnityEngine;

public class UpstreamMonitor {
    private uint latestInputSequence;
    private float latestClientTimestamp;
    private float latestReceiveTimestamp;
    private uint latestServerTickAck;

    private float clientUpstreamJitter;
    private float clientUpstreamPacketLoss;

    private static int jitterWindow => NetworkSettings.tickRate;
    private float[] jitterSamples = new float[jitterWindow];
    private float[] jitterSorted = new float[jitterWindow];
    private int jitterIndex;
    private int jitterCount;

    private static int lossWindow => NetworkSettings.tickRate;
    private readonly bool[] received = new bool[lossWindow];
    private int receivedCount;
    private bool hasTick;

    private float lastLatency;
    private bool hasLastLatency;

    public void ProcessHeader(InputHeader header) {
        latestClientTimestamp = header.clientSendTime;
        latestReceiveTimestamp = FixedClock.GetTime();

        float currentLatency = latestReceiveTimestamp - header.clientSendTime;

        UpdatePacketLoss(header.inputSequence);
        SampleJitter(currentLatency);

        latestInputSequence = header.inputSequence;
        latestServerTickAck = header.serverTickAck;
    }

    private void SampleJitter(float latency) {
        if (!hasLastLatency) {
            hasLastLatency = true;
            lastLatency = latency;
            return;
        }

        float delta = Mathf.Abs(latency - lastLatency);
        lastLatency = latency;

        jitterSamples[jitterIndex] = delta;
        jitterIndex = (jitterIndex + 1) % jitterWindow;

        if (jitterCount < jitterWindow) {
            jitterCount++;
        }

        Array.Copy(jitterSamples, jitterSorted, jitterCount);
        Array.Sort(jitterSorted, 0, jitterCount);

        int p95Index = Mathf.CeilToInt(jitterCount * 0.95f) - 1;
        p95Index = Mathf.Clamp(p95Index, 0, jitterCount - 1);

        clientUpstreamJitter = jitterSorted[p95Index];
    }

    private void UpdatePacketLoss(uint sequence) {
        if (!hasTick) {
            hasTick = true;
            latestInputSequence = sequence;
        }

        if (sequence <= latestInputSequence) {
            return;
        }

        uint missedTicks = (uint)Mathf.Min(sequence - latestInputSequence - 1, lossWindow);
        for (uint i = 1; i <= missedTicks; i++) {
            int slot = (int)((latestInputSequence + i) % lossWindow);
            if (received[slot]) {
                received[slot] = false;
                receivedCount--;
            }
        }

        int arrivedSlot = (int)(sequence % lossWindow);
        if (!received[arrivedSlot]) {
            receivedCount++;
            received[arrivedSlot] = true;
        }

        latestInputSequence = sequence;

        float sampleLoss = 1f - (receivedCount / (float)lossWindow);
        clientUpstreamPacketLoss = sampleLoss;
    }

    public UpstreamStatistics GetUpstreamStatistics() {
        return new UpstreamStatistics {
            jitter = clientUpstreamJitter,
            packetLoss = clientUpstreamPacketLoss
        };
    }

    public uint GetLatestInputSequence() {
        return latestInputSequence;
    }

    public float GetLatestClientTimestamp() {
        return latestClientTimestamp;
    }

    public float GetLatestReceiveTimestamp() {
        return latestReceiveTimestamp;
    }

    public uint GetLatestServerTickAck() {
        return latestServerTickAck;
    }
}