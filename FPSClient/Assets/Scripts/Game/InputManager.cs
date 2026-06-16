using System.Collections.Generic;
using UnityEngine;

public class InputManager : MonoBehaviour {
    public static PlayerInput[] inputHistory = new PlayerInput[NetworkSettings.inputBufferSize];

    public static uint lastSentTick;

    private static List<PlayerInput> playerInputs = new();

    private static float x;
    private static float y;
    private static bool jumping;
    private static bool crouching;
    private static bool shoot;

    public void Update() {
        SampleInput();
    }

    private void SampleInput() {
        x = Input.GetAxisRaw("Horizontal");
        y = Input.GetAxisRaw("Vertical");
        jumping = Input.GetButton("Jump");
        crouching = Input.GetButton("Crouch");
        shoot = Input.GetKey(KeyCode.Mouse0);
    }

    public PlayerInput GatherInput(uint tick) {
        PlayerInput input = new PlayerInput {
            tick = tick,
            renderTick = SnapshotManager.clientRenderTick,
            x = x,
            y = y,
            yaw = LocalPlayer.Instance.movement.desiredX,
            pitch = LocalPlayer.Instance.movement.cameraRot.x,
            jumping = jumping,
            crouching = crouching,
            shoot = shoot,
        };

        uint i = input.tick % NetworkSettings.inputBufferSize;
        inputHistory[i] = input;

        return input;
    }

    public static void SendPlayerInputs() {
        if (LocalPlayer.Instance == null) return;

        int bufferSize = NetworkSettings.inputBufferSize;

        if (TickTimer.tick == 0) return;
        uint lastCompletedTick = TickTimer.tick - 1;

        uint firstUnsents = lastSentTick + 1;

        playerInputs.Clear();

        if (firstUnsents <= lastCompletedTick)
            for (uint t = firstUnsents; t <= lastCompletedTick; t++) {
                PlayerInput input = inputHistory[t % bufferSize];
                if (input != null && input.tick == t)
                    playerInputs.Add(input);
            }

        for (uint i = 0; i < NetworkSettings.inputRedundancy; i++) {
            uint inputTick = lastCompletedTick - i;

            if (inputTick >= firstUnsents) continue;

            PlayerInput input = inputHistory[inputTick % bufferSize];
            if (input != null && input.tick == inputTick)
                playerInputs.Add(input);
        }

        if (playerInputs.Count == 0) return;

        playerInputs.Sort((a, b) => a.tick.CompareTo(b.tick));


        ClientSend.PlayerInput(playerInputs);

        lastSentTick = lastCompletedTick;
    }

    public void ProcessInput(PlayerInput input) {
        PlayerPrediction.Instance.PredictState(input);
        if (input.shoot) LocalPlayer.Instance.weaponController.Shoot();
    }
}