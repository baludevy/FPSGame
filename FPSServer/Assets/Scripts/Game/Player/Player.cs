using UnityEngine;

public class Player : MonoBehaviour {
    public int id;
    public string username;

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

    public void HandleInput(PlayerInput input) {
        movement.SetInput(input);
        invoker.Step();
        playerCam.rotation = Quaternion.Euler(input.pitch, input.yaw, 0);

        if (input.shoot) {
            weaponController.Shoot(input, GameManager.Instance.lagCompensation);
        }

        movement.AdvanceLogic();
    }

    public PlayerState GetState() {
        return new PlayerState {
            id = id,
            position = transform.position,
            crouching = movement.IsCrouching()
        };
    }
}