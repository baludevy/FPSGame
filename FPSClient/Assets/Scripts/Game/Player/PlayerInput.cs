using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInput : MonoBehaviour {
    public static InputData[] inputHistory = new InputData[NetworkSettings.inputHistorySize];

    public uint lastSentTick;

    private static List<InputData> playerInputs = new();

    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference jumpAction;
    [SerializeField] private InputActionReference crouchAction;
    [SerializeField] private InputActionReference shootAction;

    private float x;
    private float y;
    private bool jumping;
    private bool crouching;
    private bool shoot;

    private void OnEnable() {
        moveAction.action.Enable();
        jumpAction.action.Enable();
        crouchAction.action.Enable();
        shootAction.action.Enable();
    }

    private void OnDisable() {
        moveAction.action.Disable();
        jumpAction.action.Disable();
        crouchAction.action.Disable();
        shootAction.action.Disable();
    }

    public void SampleInput() {
        Vector2 move = moveAction.action.ReadValue<Vector2>();
        x = move.x;
        y = move.y;
        jumping |= jumpAction.action.IsPressed();
        crouching |= crouchAction.action.IsPressed();
        shoot |= shootAction.action.IsPressed();
    }

    public void ConsumeInput() {
        jumping = jumpAction.action.IsPressed();
        crouching = crouchAction.action.IsPressed();
        shoot = shootAction.action.IsPressed();
    }

    public InputData GatherInput(uint tick) {
        Buttons buttons = Buttons.None;

        if (jumping) buttons |= Buttons.Jump;
        if (crouching) buttons |= Buttons.Crouch;
        if (shoot) buttons |= Buttons.Shoot;

        InputData inputData = new InputData {
            tick = tick,
            renderTick = 0,
            x = x,
            y = y,
            pitch = LocalPlayer.Instance.playerCamera.GetCameraRot().x,
            yaw = LocalPlayer.Instance.playerCamera.GetCameraRot().y,
            buttons = buttons,
        };

        uint i = inputData.tick % NetworkSettings.inputHistorySize;
        inputHistory[i] = inputData;

        return inputData;
    }

    public void SendPlayerInputs() {
        if (LocalPlayer.Instance == null || !Client.IsConnected) return;

        int bufferSize = NetworkSettings.inputHistorySize;

        if (FixedClock.tick == 0) return;
        uint lastCompletedTick = FixedClock.tick - 1;

        uint firstUnsent = lastSentTick + 1;

        playerInputs.Clear();

        if (firstUnsent <= lastCompletedTick)
            for (uint t = firstUnsent; t <= lastCompletedTick; t++) {
                InputData inputData = inputHistory[t % bufferSize];
                if (inputData != null && inputData.tick == t)
                    playerInputs.Add(inputData);
            }

        for (uint i = 0; i < NetworkSettings.inputRedundancy; i++) {
            uint inputTick = lastCompletedTick - i;

            if (inputTick >= firstUnsent) continue;

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