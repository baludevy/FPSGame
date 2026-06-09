using System.Collections.Generic;
using UnityEngine;

public class InputBuffer {
    private PlayerInput[] inputQueue;

    public double latestTimestamp;

    public void Initialize() {
        inputQueue = new PlayerInput[NetworkSettings.inputBufferSize];
    }

    public PlayerInput GetInputFromQueue(int tick) {
        PlayerInput input = inputQueue[tick % NetworkSettings.inputBufferSize];

        if (input == null) return new PlayerInput();

        if (input.tick != tick) return new PlayerInput();
        
        // Debug.Log($"pulled input from queue, tick: {tick} arrived: {input.arrivedTick}");

        return input;
    }

    public void AddInputsToQueue(List<PlayerInput> inputs, double timestamp) {
        latestTimestamp = timestamp;

        foreach (PlayerInput input in inputs) {
            int i = input.tick % NetworkSettings.inputBufferSize;

            if (inputQueue[i] != null && inputQueue[i].tick == input.tick) continue;

            inputQueue[i] = input;
        }
    }

    public byte GetBufferSlack() {
        byte continuousPackets = 0;
        
        for (int i = 1; i <= NetworkSettings.inputBufferSize; i++) {
            int checkTick = NetworkManager.tick + i;
            PlayerInput input = inputQueue[checkTick % NetworkSettings.inputBufferSize];
            
            if (input != null && input.tick == checkTick) {
                continuousPackets++;
            } else {
                break;
            }
        }
        
        return continuousPackets;
    }
}