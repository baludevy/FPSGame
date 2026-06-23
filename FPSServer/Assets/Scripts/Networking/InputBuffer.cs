using System;
using System.Collections.Generic;
using UnityEngine;

public class InputBuffer {
    private InputData[] inputQueue;
    private float[] calculatedMargins;
    private InputData lastValidInputData;

    public uint latestTick;
    public float latestTimestamp;
    public float latestReceived;

    public float serverReceiveMargin;
    private float marginSmoothing = 0.1f;
    private bool hasMargin;

    public float serverInputJitter;
    private static int jitterWindow = 128;
    private float[] jitterSamples = new float[jitterWindow];
    private float[] jitterSorted = new float[jitterWindow];
    private int jitterIndex;
    private int jitterCount;
    private float intervalSmoothing = 0.05f;

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
            SampleMargin(calculatedMargins[i]);
            lastValidInputData = inputData;
            return inputData;
        }

        int missingTicks = (int)tick - (int)latestTick;
        float estimatedPacketDueTime = latestReceived + (missingTicks * NetworkSettings.tickTime);
        float realTimeDeficit = FixedClock.GetTime() - estimatedPacketDueTime;

        SampleMargin(realTimeDeficit);

        InputData fallbackInputData = new InputData {
            tick = tick,
            renderTick = lastValidInputData.renderTick,
            x = lastValidInputData.x,
            y = lastValidInputData.y,
            crouching = lastValidInputData.crouching,
            pitch = lastValidInputData.pitch,
            yaw = lastValidInputData.yaw,
        };

        Debug.Log($"{tick}: returning fallback input");

        return fallbackInputData;
    }

    public void AddInputsToQueue(List<InputData> inputs, float clientSendTime) {
        latestTimestamp = clientSendTime;
        latestReceived = FixedClock.GetTime();

        uint previousLatest = latestTick;
        uint newestInBatch = previousLatest;

        foreach (InputData input in inputs) {
            if (input.tick > newestInBatch) newestInBatch = input.tick;

            uint i = input.tick % (uint)NetworkSettings.inputBufferSize;

            if (inputQueue[i] != null && inputQueue[i].tick == input.tick) continue;

            inputQueue[i] = input;

            float scheduledTickTime = input.tick * NetworkSettings.tickTime;
            calculatedMargins[i] = scheduledTickTime - latestReceived;
        }

        if (newestInBatch > previousLatest) {
            latestTick = newestInBatch;
        }

        float currentLatency = latestReceived - clientSendTime;

        SampleJitter(currentLatency);
    }

    private void SampleMargin(float rawMargin) {
        if (!hasMargin) {
            hasMargin = true;
            serverReceiveMargin = rawMargin;
            return;
        }

        serverReceiveMargin = (rawMargin * marginSmoothing) + (serverReceiveMargin * (1f - marginSmoothing));
    }

    private void SampleJitter(float currentLatency) {
        if (!hasLastLatency) {
            hasLastLatency = true;
            smoothedLatency = currentLatency;
            return;
        }

        smoothedLatency = (currentLatency * intervalSmoothing) + (smoothedLatency * (1f - intervalSmoothing));

        float delta = Mathf.Abs(currentLatency - smoothedLatency);

        float oldSample = jitterSamples[jitterIndex];
        jitterSamples[jitterIndex] = delta;
        jitterIndex = (jitterIndex + 1) % jitterWindow;

        if (jitterCount < jitterWindow) {
            jitterCount++;
            int insertIndex = Array.BinarySearch(jitterSorted, 0, jitterCount - 1, delta);
            if (insertIndex < 0) insertIndex = ~insertIndex;

            Array.Copy(jitterSorted, insertIndex, jitterSorted, insertIndex + 1, (jitterCount - 1) - insertIndex);
            jitterSorted[insertIndex] = delta;
        }
        else {
            int removeIndex = Array.BinarySearch(jitterSorted, 0, jitterWindow, oldSample);
            if (removeIndex >= 0) {
                Array.Copy(jitterSorted, removeIndex + 1, jitterSorted, removeIndex, jitterWindow - removeIndex - 1);
            }

            int insertIndex = Array.BinarySearch(jitterSorted, 0, jitterWindow - 1, delta);
            if (insertIndex < 0) insertIndex = ~insertIndex;

            Array.Copy(jitterSorted, insertIndex, jitterSorted, insertIndex + 1, (jitterWindow - 1) - insertIndex);
            jitterSorted[insertIndex] = delta;
        }

        int p95Index = Mathf.CeilToInt(jitterCount * 0.95f) - 1;
        p95Index = Mathf.Clamp(p95Index, 0, jitterCount - 1);
        serverInputJitter = jitterSorted[p95Index];
    }
}