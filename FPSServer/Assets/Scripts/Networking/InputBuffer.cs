using System.Collections.Generic;
using UnityEngine;

public class InputBuffer {
    private InputData[] inputQueue;
    private InputData lastValidInputData;

    public uint latestTick;
    public float latestTimestamp;
    public float latestReceived;

    public void Initialize() {
        inputQueue = new InputData[NetworkSettings.inputBufferSize];
        lastValidInputData = new InputData();
    }

    public InputData GetInputFromQueue(uint tick) {
        InputData inputData = inputQueue[tick % NetworkSettings.inputBufferSize];

        if (inputData != null && inputData.tick == tick) {
            lastValidInputData = inputData;
            return inputData;
        }

        InputData fallbackInputData = new InputData {
            tick = tick,
            renderTick = lastValidInputData.renderTick,
            x = lastValidInputData.x,
            y = lastValidInputData.y,
            crouching = lastValidInputData.crouching,
            pitch = lastValidInputData.pitch,
            yaw = lastValidInputData.yaw,
        };

        Debug.Log($"returning fallback input: {tick}");
        
        return fallbackInputData;
    }

    public void AddInputsToQueue(List<InputData> inputs, float timestamp) {
        latestTimestamp = timestamp;
        latestReceived = FixedClock.GetTime();

        foreach (InputData input in inputs) {
            if (input.tick > latestTick) latestTick = input.tick;

            uint i = input.tick % NetworkSettings.inputBufferSize;

            if (inputQueue[i] != null && inputQueue[i].tick == input.tick) continue;

            inputQueue[i] = input;
        }
    }

    public sbyte GetBufferSize() {
        long offset = (long)latestTick - FixedClock.tick;
        return (sbyte)Mathf.Clamp((int)offset, sbyte.MinValue, sbyte.MaxValue);
    }
}