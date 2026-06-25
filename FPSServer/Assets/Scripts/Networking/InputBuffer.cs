using System;
using System.Collections.Generic;
using UnityEngine;

public class InputBuffer {
    private InputData[] inputQueue;
    private float[] calculatedMargins;
    private InputData lastValidInputData;

    private uint latestClientTick;
    private uint latestInputSequence;
    private float latestClientTimestamp;
    private float latestReceiveTimestamp;

    private float inputReceiveMargin;

    private float clientUpstreamJitter;
    private float clientUpstreamPacketLoss;

    private static int jitterWindow = 128;
    private float[] jitterSamples = new float[jitterWindow];
    private float[] jitterSorted = new float[jitterWindow];
    private int jitterIndex;
    private int jitterCount;
    private float intervalSmoothing = 0.1f;

    private static int lossWindow = 128;
    private readonly bool[] received = new bool[lossWindow];
    private int receivedCount;
    private bool hasTick;

    private float smoothedLatency;
    private bool hasLastLatency;

    public void Initialize() {
        inputQueue = new InputData[NetworkSettings.inputBufferSize];
        calculatedMargins = new float[NetworkSettings.inputBufferSize];
        lastValidInputData = new InputData();
    }

    public InputData GetInputFromQueue(uint tick) {
        uint i = tick % NetworkSettings.inputBufferSize;
        InputData inputData = inputQueue[i];

        if (inputData != null && inputData.tick == tick) {
            inputReceiveMargin = calculatedMargins[i];
            lastValidInputData = inputData;
            return inputData;
        }

        int missingTicks = (int)tick - (int)latestClientTick;
        float estimatedPacketDueTime = latestReceiveTimestamp + (missingTicks * NetworkSettings.tickTime);

        inputReceiveMargin = FixedClock.GetTime() - estimatedPacketDueTime;

        InputData fallbackInputData = new InputData {
            tick = tick,
            renderTick = lastValidInputData.renderTick,
            x = lastValidInputData.x,
            y = lastValidInputData.y,
            pitch = lastValidInputData.pitch,
            yaw = lastValidInputData.yaw,
        };

        Debug.Log($"{tick}: returning fallback input");

        return fallbackInputData;
    }

    public void AddInputsToQueue(List<InputData> inputs, uint sequence, float clientSendTime) {
        latestClientTimestamp = clientSendTime;
        latestReceiveTimestamp = FixedClock.GetTime();

        uint previousLatest = latestClientTick;
        uint newestInBatch = previousLatest;

        foreach (InputData input in inputs) {
            if (input.tick > newestInBatch) newestInBatch = input.tick;

            uint i = input.tick % NetworkSettings.inputBufferSize;

            if (inputQueue[i] != null && inputQueue[i].tick == input.tick) continue;

            inputQueue[i] = input;

            float scheduledTickTime = input.tick * NetworkSettings.tickTime;
            calculatedMargins[i] = scheduledTickTime - latestReceiveTimestamp;
        }

        float currentLatency = latestReceiveTimestamp - clientSendTime;
        
        UpdatePacketLoss(sequence);
        SampleJitter(currentLatency);

        latestInputSequence = sequence;

        if (newestInBatch > previousLatest) {
            latestClientTick = newestInBatch;
        }
    }

    private void SampleJitter(float currentLatency) {
        if (!hasLastLatency) {
            hasLastLatency = true;
            smoothedLatency = currentLatency;
            return;
        }

        float delta = Mathf.Abs(currentLatency - smoothedLatency);
        smoothedLatency = currentLatency;

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

        if (sequence <= latestInputSequence) return;

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

    public TimingInfo GetTimingInfo() {
        return new TimingInfo {
            inputReceiveMargin = inputReceiveMargin,
            clientSendTimeAck = latestClientTimestamp,
            serverSendTime = FixedClock.GetTime(),
            serverReceiveTime = latestReceiveTimestamp,
        };
    }

    public UpstreamStatistics GetUpstreamStatistics() {
        return new UpstreamStatistics {
            jitter = clientUpstreamJitter,
            packetLoss = clientUpstreamPacketLoss
        };
    }

    public uint GetLatestSequence() {
        return latestInputSequence;
    }
}