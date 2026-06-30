using System.Collections.Generic;
using UnityEngine;

public class InputBuffer {
    private Player player;
    private UpstreamMonitor monitor;

    private InputData[] inputQueue;
    private float[] calculatedMargins;
    private float[] arrivalTimestamps;
    private InputData lastValidInputData;

    private uint latestClientTick;

    private float inputReceiveMargin;

    // logs
    private int fallbackCount;
    private float fallbackStartTime;
    private bool isTrackingFallback;

    private int rejectedCount;
    private float rejectedStartTime;
    private bool isTrackingRejected;

    public void Initialize(Player player, UpstreamMonitor monitor) {
        this.player = player;
        this.monitor = monitor;
        inputQueue = new InputData[NetworkSettings.inputBufferSize];
        calculatedMargins = new float[NetworkSettings.inputBufferSize];
        arrivalTimestamps = new float[NetworkSettings.inputBufferSize];
        lastValidInputData = new InputData();
    }

    public InputData GetInputFromQueue(uint tick) {
        uint i = tick % NetworkSettings.inputBufferSize;
        InputData inputData = inputQueue[i];

        float currentTime = FixedClock.GetTime();

        if (inputData != null && inputData.tick == tick) {
            inputReceiveMargin = currentTime - arrivalTimestamps[i];
            calculatedMargins[i] = inputReceiveMargin;
            lastValidInputData = inputData;

            CheckAndLogFallback();

            return inputData;
        }

        if (!isTrackingFallback) {
            isTrackingFallback = true;
            fallbackStartTime = currentTime;
            fallbackCount = 0;
        }

        fallbackCount++;

        float timeElapsedSinceLastPacket = currentTime - monitor.GetLatestReceiveTimestamp();
        int missingTicks = (int)tick - (int)latestClientTick;

        float expectedElapsedNetworkTime = missingTicks * NetworkSettings.tickTime;

        inputReceiveMargin = timeElapsedSinceLastPacket - expectedElapsedNetworkTime;

        InputData fallbackInputData = new InputData {
            tick = tick,
            x = lastValidInputData.x,
            y = lastValidInputData.y,
            pitch = lastValidInputData.pitch,
            yaw = lastValidInputData.yaw,
            buttons = lastValidInputData.buttons
        };

        return fallbackInputData;
    }

    public void AddInputsToQueue(List<InputData> inputs) {
        float receiveTimestamp = monitor.GetLatestReceiveTimestamp();

        uint previousLatest = latestClientTick;
        uint newestInBatch = previousLatest;

        bool batchHadValidInput = false;

        foreach (InputData input in inputs) {
            if (input.tick > newestInBatch) {
                newestInBatch = input.tick;
            }

            uint i = input.tick % NetworkSettings.inputBufferSize;

            float inputTimeLocation = input.tick * NetworkSettings.tickTime;

            // reject inputs that are too far into the future
            if (FixedClock.tick < input.tick && inputTimeLocation - FixedClock.tick * NetworkSettings.tickTime >
                NetworkSettings.maxInputMarginTime) {
                if (!isTrackingRejected) {
                    isTrackingRejected = true;
                    rejectedStartTime = receiveTimestamp;
                    rejectedCount = 0;
                }

                rejectedCount++;

                calculatedMargins[i] = inputTimeLocation - receiveTimestamp;
                continue;
            }

            if (inputQueue[i] != null && inputQueue[i].tick == input.tick) {
                continue;
            }

            inputQueue[i] = input;
            arrivalTimestamps[i] = receiveTimestamp;
            batchHadValidInput = true;
        }

        if (batchHadValidInput) {
            CheckAndLogRejected();
        }

        if (newestInBatch > previousLatest) {
            latestClientTick = newestInBatch;
        }
    }

    private void CheckAndLogFallback() {
        if (isTrackingFallback) {
            if (fallbackCount >= 10) {
                float duration = FixedClock.GetTime() - fallbackStartTime;
                Debug.LogWarning(
                    $"Client lost {fallbackCount} inputs, fallback inputs for {player.username} ({player.id}) were used over a time of {duration:F1}s");
            }

            isTrackingFallback = false;
            fallbackCount = 0;
        }
    }

    private void CheckAndLogRejected() {
        if (isTrackingRejected) {
            if (rejectedCount >= 10) {
                float duration = monitor.GetLatestReceiveTimestamp() - rejectedStartTime;
                Debug.LogWarning(
                    $"Rejected {rejectedCount} future inputs from {player.username} ({player.id}) over a time of {duration:F1}s");
            }

            isTrackingRejected = false;
            rejectedCount = 0;
        }
    }

    public TimingInfo GetTimingInfo() {
        return new TimingInfo {
            inputReceiveMargin = inputReceiveMargin,
            clientSendTimeAck = monitor.GetLatestClientTimestamp(),
            serverSendTime = FixedClock.GetTime(),
            serverReceiveTime = monitor.GetLatestReceiveTimestamp(),
        };
    }
}