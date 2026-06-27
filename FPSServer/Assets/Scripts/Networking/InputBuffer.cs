using System;
using System.Collections.Generic;
using UnityEngine;

public class InputBuffer {
    private Player player;
    
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

    // logs
    private int fallbackCount;
    private float fallbackStartTime;
    private bool isTrackingFallback;

    private int rejectedCount;
    private float rejectedStartTime;
    private bool isTrackingRejected;

    public void Initialize(Player player) {
        this.player = player;
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
            
            CheckAndLogFallback();

            return inputData;
        }

        float currentTime = FixedClock.GetTime();
        if (!isTrackingFallback) {
            isTrackingFallback = true;
            fallbackStartTime = currentTime;
            fallbackCount = 0;
        }

        fallbackCount++;

        int missingTicks = (int)tick - (int)latestClientTick;
        float estimatedPacketDueTime = latestReceiveTimestamp + missingTicks * NetworkSettings.tickTime;

        inputReceiveMargin = currentTime - estimatedPacketDueTime;

        InputData fallbackInputData = new InputData {
            tick = tick,
            renderTick = lastValidInputData.renderTick,
            x = lastValidInputData.x,
            y = lastValidInputData.y,
            pitch = lastValidInputData.pitch,
            yaw = lastValidInputData.yaw,
            buttons = lastValidInputData.buttons
        };

        return fallbackInputData;
    }

    public void AddInputsToQueue(List<InputData> inputs, uint sequence, float clientSendTime) {
        latestClientTimestamp = clientSendTime;
        latestReceiveTimestamp = FixedClock.GetTime();

        uint previousLatest = latestClientTick;
        uint newestInBatch = previousLatest;

        bool batchHadValidInput = false;

        foreach (InputData input in inputs) {
            if (input.tick > newestInBatch) newestInBatch = input.tick;

            uint i = input.tick % NetworkSettings.inputBufferSize;

            float inputTimeLocation = input.tick * NetworkSettings.tickTime;

            // reject inputs that are too far into the future
            if (FixedClock.tick < input.tick && inputTimeLocation - FixedClock.tick * NetworkSettings.tickTime >
                NetworkSettings.maxInputMarginTime) {
                if (!isTrackingRejected) {
                    isTrackingRejected = true;
                    rejectedStartTime = latestReceiveTimestamp;
                    rejectedCount = 0;
                }

                rejectedCount++;

                calculatedMargins[i] = inputTimeLocation - latestReceiveTimestamp;
                continue;
            }

            if (inputQueue[i] != null && inputQueue[i].tick == input.tick) continue;
            inputQueue[i] = input;
            calculatedMargins[i] = inputTimeLocation - latestReceiveTimestamp;
            batchHadValidInput = true;
        }
        
        if (batchHadValidInput) {
            CheckAndLogRejected();
        }

        float currentLatency = latestReceiveTimestamp - clientSendTime;

        UpdatePacketLoss(sequence);
        SampleJitter(currentLatency);

        latestInputSequence = sequence;

        if (newestInBatch > previousLatest) {
            latestClientTick = newestInBatch;
        }
    }

    private void CheckAndLogFallback() {
        if (isTrackingFallback) {
            if (fallbackCount >= 5) {
                float duration = FixedClock.GetTime() - fallbackStartTime;
                Debug.LogWarning($"Used {fallbackCount} fallback inputs for {player.username} ({player.id}) over a time of {duration:F1}s");
            }

            isTrackingFallback = false;
            fallbackCount = 0;
        }
    }

    private void CheckAndLogRejected() {
        if (isTrackingRejected) {
            if (rejectedCount >= 5) {
                float duration = latestReceiveTimestamp - rejectedStartTime;
                Debug.LogWarning($"Rejected {rejectedCount} future inputs from {player.username} ({player.id}) over a time of {duration:F1}s");
            }

            isTrackingRejected = false;
            rejectedCount = 0;
        }
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
}