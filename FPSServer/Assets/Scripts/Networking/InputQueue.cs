using UnityEngine;

public class InputQueue {
    private PlayerInput[] inputQueue;

    public void Initialize() {
        inputQueue = new PlayerInput[NetworkSettings.inputBufferSize];
    }

    public PlayerInput GetInputFromQueue(int tick) {
        return inputQueue[tick % NetworkSettings.inputBufferSize];
    }

    public void AddInputToQueue(PlayerInput input) {
        int i = NetworkManager.tick % NetworkSettings.inputBufferSize;

        inputQueue[i] = input;
    }
}