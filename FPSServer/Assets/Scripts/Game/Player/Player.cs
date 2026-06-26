using UnityEngine;

public class Player : MonoBehaviour {
    [HideInInspector] public int id;
    [HideInInspector] public string username;

    public TickInvoker invoker;
    public InputBuffer inputBuffer;
    public PlayerMovement movement;
    public WeaponController weaponController;
    public Transform playerCam;

    public void Initialize(int id, string username) {
        this.id = id;
        this.username = username;

        inputBuffer = new InputBuffer();
        inputBuffer.Initialize();
        invoker = new TickInvoker();
    }

    public void HandleInput(InputData inputData) {
        invoker.Step();
        movement.SetInput(inputData);
        playerCam.rotation = Quaternion.Euler(inputData.pitch, inputData.yaw, 0);

        if ((inputData.buttons & Buttons.Shoot) != 0) {
            Debug.Log($"{movement.transform.position} tick: {inputData.tick}");
            weaponController.Shoot(inputData);
        }

        movement.AdvanceLogic();
    }

    public PlayerState GetState() {
        return new PlayerState {
            id = id,
            position = movement.transform.position,
            crouching = movement.IsCrouching()
        };
    }
}