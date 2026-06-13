using System.Collections.Generic;
using UnityEngine;

public class InputBuffer {
    private PlayerInput[] inputQueue;
    private PlayerInput lastValidInput; 

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
        
        // Debug.Log("returnign fallback input");

        PlayerInput fallbackInput = new PlayerInput();
        fallbackInput.tick = tick;
        
        return fallbackInput;
    }

    public void AddInputsToQueue(List<PlayerInput> inputs, float timestamp) {
        latestTimestamp = timestamp;
        latestReceived = (float)NetworkManager.Instance.GetTime();

        foreach (PlayerInput input in inputs) { 
            uint i = input.tick % NetworkSettings.inputBufferSize;

            if (inputQueue[i] != null && inputQueue[i].tick == input.tick) continue;

            inputQueue[i] = input;
        }
    }

    public sbyte GetBufferOffset() {
        uint highestTickInQueue = 0;
        bool queueHasData = false;

        for (int i = 0; i < NetworkSettings.inputBufferSize; i++) {
            if (inputQueue[i] != null) {
                if (!queueHasData || inputQueue[i].tick > highestTickInQueue) {
                    highestTickInQueue = inputQueue[i].tick;
                    queueHasData = true;
                }
            }
        }

        if (!queueHasData || highestTickInQueue < NetworkManager.tick) {
            int missingPackets = 0;
            for (uint i = 0; i < NetworkSettings.inputBufferSize; i++) {
                uint checkTick = NetworkManager.tick - i;
                PlayerInput input = inputQueue[checkTick % NetworkSettings.inputBufferSize];

                if (input == null || input.tick != checkTick) {
                    missingPackets--;
                }
            }
            return (sbyte)Mathf.Clamp(missingPackets, sbyte.MinValue, sbyte.MaxValue);
        }

        int trueSlack = (int)(highestTickInQueue - NetworkManager.tick);
        return (sbyte)Mathf.Clamp(trueSlack, sbyte.MinValue, sbyte.MaxValue);
    }
}