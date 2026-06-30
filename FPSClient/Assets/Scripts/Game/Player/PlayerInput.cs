using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInput : MonoBehaviour {
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference jumpAction;
    [SerializeField] private InputActionReference crouchAction;
    [SerializeField] private InputActionReference shootAction;

    private static InputData[] inputHistory;

    private uint inputSequence;
    private uint lastSentTick;

    private static List<InputData> playerInputs = new();

    private float x;
    private float y;
    private bool jumping;
    private bool crouching;

    private void Awake() {
        inputHistory = new InputData[NetworkSettings.inputHistorySize];
        
        for (int i = 0; i < inputHistory.Length; i++) {
            inputHistory[i].tick = uint.MaxValue;
        }
    }

    public void SampleInput() {
        Vector2 move = moveAction.action.ReadValue<Vector2>();
        x = move.x;
        y = move.y;

        jumping |= jumpAction.action.IsPressed();
        crouching |= crouchAction.action.IsPressed();
    }

    public void ConsumeInput() {
        jumping = jumpAction.action.IsPressed();
        crouching = crouchAction.action.IsPressed();
    }

    public InputData GatherInput(uint tick) {
        Buttons buttons = Buttons.None;

        if (jumping) buttons |= Buttons.Jump;
        if (crouching) buttons |= Buttons.Crouch;

        Vector2 look = Player.Instance.camera.GetCameraRot();

        InputData inputData = new InputData {
            tick = tick,
            x = x,
            y = y,
            pitch = look.x,
            yaw = look.y,
            buttons = buttons
        };

        uint i = inputData.tick % NetworkSettings.inputHistorySize;
        inputHistory[i] = inputData;

        return inputData;
    }

    public void SendPlayerInputs() {
        if (FixedClock.tick == 0) {
            return;
        }

        uint bufferSize = NetworkSettings.inputHistorySize;
        uint lastCompletedTick = FixedClock.tick - 1;
        uint redundancy = NetcodeState.inputRedundancy;

        uint start;
        if (lastCompletedTick >= redundancy) {
            start = lastCompletedTick - redundancy;
        }
        else {
            start = 0;
        }

        playerInputs.Clear();

        for (uint t = start; t <= lastCompletedTick; t++) {
            ref InputData inputData = ref inputHistory[t % bufferSize];
            if (inputData.tick == t) {
                playerInputs.Add(inputData);
            }
        }

        if (playerInputs.Count == 0) {
            return;
        }

        InputHeader header = new InputHeader {
            inputSequence = inputSequence,
            serverTickAck = UpdateDeserializer.latestTick,
            clientSendTime = FixedClock.GetTime(),
        };

        ClientSend.PlayerInput(header, playerInputs);

        inputSequence++;
        lastSentTick = lastCompletedTick;
    }

    public static InputData[] GetInputHistory() {
        return inputHistory;
    }

    public uint GetLastSentTick() {
        return lastSentTick;
    }

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
}