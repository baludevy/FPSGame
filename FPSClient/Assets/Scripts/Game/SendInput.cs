using System;
using System.Collections.Generic;
using UnityEngine;

public class SendInput : MonoBehaviour {
    public static SendInput Instance;

    public PlayerInput[] inputHistory = new PlayerInput[NetworkSettings.inputBufferSize];

    public int lastSentTick;
    
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
    
    public PlayerInput GatherInput(int tick) {
        PlayerInput input = new PlayerInput {
            tick = tick,
            x = x,
            y = y,
            orientation = PlayerMovement.Instance.orientation.eulerAngles.y,
            crouching = crouching,
            jumping = jumping
        };

        int i = input.tick % NetworkSettings.inputBufferSize;
        inputHistory[i] = input;

        return input;
    }
    
    public void SendPlayerInputs() {
        if (PlayerMovement.Instance == null) return;
        if (Instance == null) return;

        int bufferSize = NetworkSettings.inputBufferSize;

        if (TickTimer.tick == 0) return;
        int lastCompletedTick = TickTimer.tick - 1;

        int firstUnsents = lastSentTick + 1;

        playerInputs.Clear();

        if (firstUnsents <= lastCompletedTick)
            for (int t = firstUnsents; t <= lastCompletedTick; t++) {
                PlayerInput input = Instance.inputHistory[t % bufferSize];
                if (input != null && input.tick == t)
                    playerInputs.Add(input);
            }

        for (int i = 0; i < NetworkSettings.inputRedundancy; i++) {
            int inputTick = lastCompletedTick - i;

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