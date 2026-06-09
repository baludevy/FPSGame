using System.Collections.Generic;
using UnityEngine;

public class InputQueue {
    private PlayerInput[] inputQueue;

    public void Initialize() {
        inputQueue = new PlayerInput[NetworkSettings.inputBufferSize];
    }

    public PlayerInput GetInputFromQueue(int tick) {
        PlayerInput input = inputQueue[tick % NetworkSettings.inputBufferSize];

        if (input == null) return new PlayerInput();

        // ignore old inputs
        if (input.tick < NetworkManager.tick) return new PlayerInput();
        
        Debug.Log($"pulled input from queue, tick: {tick} arrived: {input.arrivedTick}");

        return input;
    }

    public void AddInputsToQueue(List<PlayerInput> inputs) {
        foreach (PlayerInput input in inputs) {
            int i = input.tick % NetworkSettings.inputBufferSize;

            Debug.Log(
                $"input tick: {input.tick} server tick: {NetworkManager.tick} delta: {input.tick - NetworkManager.tick}");

            input.arrivedTick = NetworkManager.tick;
            inputQueue[i] = input;
        }
    }
}