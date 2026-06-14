using System;
using System.Collections.Generic;
using UnityEngine;

public class SendInput : MonoBehaviour {
    public static SendInput Instance;

    public PlayerInput[] inputHistory = new PlayerInput[NetworkSettings.inputBufferSize];

    public uint lastSentTick;

    private List<PlayerInput> playerInputs = new();

    private float x;
    private float y;
    private bool jumping;
    private bool crouching;

    private void Awake() {
        Instance = this;
    }

    public void Update() {
        SampleInput();
    }

    private void SampleInput() {
        x = Input.GetAxisRaw("Horizontal");
        y = Input.GetAxisRaw("Vertical");
        jumping = Input.GetButton("Jump");
        crouching = Input.GetButton("Crouch");
    }

    public PlayerInput GatherInput(uint tick) {
        PlayerInput input = new PlayerInput {
            tick = tick,
            x = x,
            y = y,
            orientation = PlayerMovement.Instance.desiredX,
            jumping = jumping,
            crouching = crouching
        };

        uint i = input.tick % NetworkSettings.inputBufferSize;
        inputHistory[i] = input;

        return input;
    }

    public void SendPlayerInputs() {
        if (PlayerMovement.Instance == null) return;
        if (Instance == null) return;

        int bufferSize = NetworkSettings.inputBufferSize;

        if (TickTimer.tick == 0) return;
        uint lastCompletedTick = TickTimer.tick - 1;

        uint firstUnsents = lastSentTick + 1;

        playerInputs.Clear();

        if (firstUnsents <= lastCompletedTick)
            for (uint t = firstUnsents; t <= lastCompletedTick; t++) {
                PlayerInput input = Instance.inputHistory[t % bufferSize];
                if (input != null && input.tick == t)
                    playerInputs.Add(input);
            }

        for (uint i = 0; i < NetworkSettings.inputRedundancy; i++) {
            uint inputTick = lastCompletedTick - i;

            if (inputTick >= firstUnsents) continue;

            PlayerInput input = Instance.inputHistory[inputTick % bufferSize];
            if (input != null && input.tick == inputTick)
                playerInputs.Add(input);
        }

        if (playerInputs.Count == 0) return;

        playerInputs.Sort((a, b) => a.tick.CompareTo(b.tick));
        
        
        ClientSend.PlayerInput(playerInputs);

        lastSentTick = lastCompletedTick;
    }
}