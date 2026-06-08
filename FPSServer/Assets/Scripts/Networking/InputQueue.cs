using System.Collections.Generic;
using UnityEngine;

public class InputQueue {
    private PlayerInput[] inputQueue;

    public void Initialize() {
        inputQueue = new PlayerInput[NetworkSettings.inputBufferSize];
    }

    public PlayerInput GetInputFromQueue(int tick) {
        return inputQueue[tick % NetworkSettings.inputBufferSize];
    }

    public void AddInputsToQueue(List<PlayerInput> inputs) {
        foreach (PlayerInput input in inputs) {
            int i = input.tick % NetworkSettings.inputBufferSize;

            inputQueue[i] = input;
        }
    }
}