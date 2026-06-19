using System.Collections.Generic;
using UnityEngine;

public class PlayerInput : MonoBehaviour {
    public static InputData[] inputHistory = new InputData[NetworkSettings.inputHistorySize];

    public static uint lastSentTick;

    private static List<InputData> playerInputs = new();

    private static float x;
    private static float y;
    private static bool jumping;
    private static bool crouching;
    private static bool shoot;

    public void SampleInput() {
        x = Input.GetAxisRaw("Horizontal");
        y = Input.GetAxisRaw("Vertical");
        jumping = Input.GetButton("Jump");
        crouching = Input.GetButton("Crouch");
        shoot = Input.GetKey(KeyCode.Mouse0);
    }

    public InputData GatherInput(uint tick) {
        InputData inputData = new InputData {
            tick = tick,
            renderTick = SnapshotManager.clientRenderTick,
            x = x,
            y = y,
            pitch = LocalPlayer.Instance.playerCamera.GetCameraRot().x,
            yaw = LocalPlayer.Instance.playerCamera.GetCameraRot().y,
            jumping = jumping,
            crouching = crouching,
            shoot = shoot,
        };

        uint i = inputData.tick % NetworkSettings.inputHistorySize;
        inputHistory[i] = inputData;

        return inputData;
    }

    public static void SendPlayerInputs() {
        if (LocalPlayer.Instance == null) return;

        int bufferSize = NetworkSettings.inputHistorySize;

        if (FixedClock.tick == 0) return;
        uint lastCompletedTick = FixedClock.tick - 1;

        uint firstUnsents = lastSentTick + 1;

        playerInputs.Clear();

        if (firstUnsents <= lastCompletedTick)
            for (uint t = firstUnsents; t <= lastCompletedTick; t++) {
                InputData inputData = inputHistory[t % bufferSize];
                if (inputData != null && inputData.tick == t)
                    playerInputs.Add(inputData);
            }

        for (uint i = 0; i < NetworkSettings.inputRedundancy; i++) {
            uint inputTick = lastCompletedTick - i;

            if (inputTick >= firstUnsents) continue;

            InputData inputData = inputHistory[inputTick % bufferSize];
            if (inputData != null && inputData.tick == inputTick)
                playerInputs.Add(inputData);
        }

        if (playerInputs.Count == 0) return;

        playerInputs.Sort((a, b) => a.tick.CompareTo(b.tick));


        ClientSend.PlayerInput(playerInputs);

        lastSentTick = lastCompletedTick;
    }
}