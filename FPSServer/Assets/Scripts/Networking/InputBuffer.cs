using System.Collections.Generic;
using UnityEngine;

public class InputBuffer {
    private PlayerInput[] inputQueue;
    private PlayerInput lastValidInput;

    public uint latestTick;
    public float latestTimestamp;
    public float latestReceived;

    public void Initialize() {
        inputQueue = new PlayerInput[NetworkSettings.inputBufferSize];
        lastValidInput = new PlayerInput();
    }

    public PlayerInput GetInputFromQueue(uint tick) {
        PlayerInput input = inputQueue[tick % NetworkSettings.inputBufferSize];

        if (input != null && input.tick == tick) {
            lastValidInput = input;
            return input;
        }

        PlayerInput fallbackInput = new PlayerInput {
            tick = tick
        };

        return fallbackInput;
    }

    public void AddInputsToQueue(List<PlayerInput> inputs, float timestamp) {
        latestTimestamp = timestamp;
        latestReceived = NetworkManager.Instance.GetTime();

        foreach (PlayerInput input in inputs) {
            if (input.tick > latestTick) latestTick = input.tick;

            uint i = input.tick % NetworkSettings.inputBufferSize;

            if (inputQueue[i] != null && inputQueue[i].tick == input.tick) continue;

            inputQueue[i] = input;
        }
    }

    public sbyte GetBufferOffset() {
        long offset = (long)latestTick - NetworkManager.tick;
        return (sbyte)Mathf.Clamp((int)offset, sbyte.MinValue, sbyte.MaxValue);
    }
}