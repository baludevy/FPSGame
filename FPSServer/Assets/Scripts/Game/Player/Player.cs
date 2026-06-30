using System;
using UnityEngine;

public class Player : MonoBehaviour {
    [HideInInspector] public int id;
    [HideInInspector] public string username;

    [NonSerialized] public TickInvoker invoker;
    [NonSerialized] public InputBuffer inputBuffer;
    [NonSerialized] public UpstreamMonitor monitor;
    public PlayerMovement movement;
    public Transform playerCam;

    public void Initialize(int id, string username) {
        this.id = id;
        this.username = username;

        monitor = new UpstreamMonitor();

        inputBuffer = new InputBuffer();
        inputBuffer.Initialize(this, monitor);
        
        invoker = new TickInvoker();
    }

    public void MoveInput(InputData inputData) {
        invoker.Step();

        movement.SetInput(inputData);
        playerCam.rotation = Quaternion.Euler(inputData.pitch, inputData.yaw, 0);

        movement.AdvanceLogic();
    }

    public void OtherInput(InputData inputData) {

    }

    public PlayerState GetState() {
        return new PlayerState {
            id = id,
            position = movement.transform.position,
            crouching = movement.IsCrouching()
        };
    }
}