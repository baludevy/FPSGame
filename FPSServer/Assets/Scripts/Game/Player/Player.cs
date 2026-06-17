using UnityEngine;


public class Player : MonoBehaviour {
    public int id;
    public string username;

    public TickInvoker invoker;

    public InputBuffer inputBuffer;
    public PlayerMovement movement;
    public WeaponController weaponController;

    public Transform camera;

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
        camera.rotation = Quaternion.Euler(input.pitch, input.yaw, 0);

        movement.AdvanceLogic();
    }
}