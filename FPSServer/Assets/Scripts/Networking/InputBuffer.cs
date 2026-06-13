using System.Collections.Generic;
using UnityEngine;

public class InputBuffer {
    private PlayerInput[] inputQueue;
    private PlayerInput lastValidInput; 

    public float latestTimestamp;

    public void Initialize() {
        inputQueue = new PlayerInput[NetworkSettings.inputBufferSize];
        lastValidInput = new PlayerInput(); 
    }

    public PlayerInput GetInputFromQueue(int tick) {
        PlayerInput input = inputQueue[tick % NetworkSettings.inputBufferSize];

        if (input != null && input.tick == tick) {
            lastValidInput = input;
            return input;
        }
        
        Debug.Log("returnign fallback input");

        PlayerInput fallbackInput = new PlayerInput();
        fallbackInput.tick = tick;
        
        return fallbackInput;
    }

    public void AddInputsToQueue(List<PlayerInput> inputs, float timestamp) {
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