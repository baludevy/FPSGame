using UnityEngine;

public class Player : MonoBehaviour {
    [HideInInspector] public int id;
    [HideInInspector] public string username;

    public TickInvoker invoker;
    public InputBuffer inputBuffer;
    public PlayerMovement movement;
    public WeaponController weaponController;
    public Transform playerCam;

    [SerializeField] private Vector3 cameraOffset;

    private Vector3 prevTickPosition;
    private Vector3 currTickPosition;

    public void Initialize(int id, string username) {
        this.id = id;
        this.username = username;

        inputBuffer = new InputBuffer();
        inputBuffer.Initialize(this);
        invoker = new TickInvoker();
    }

    public void MoveInput(InputData inputData) {
        invoker.Step();

        prevTickPosition = currTickPosition;

        movement.SetInput(inputData);
        playerCam.rotation = Quaternion.Euler(inputData.pitch, inputData.yaw, 0);

        movement.AdvanceLogic();
        
        // Calculate the camera's interpolated position BEFORE global physics runs.
        // This ensures prevTickPosition and currTickPosition match the boundaries of this input's tick.
        float factor = Mathf.Clamp01(inputData.interpolationFactor);
        playerCam.position = Vector3.Lerp(prevTickPosition, movement.transform.position, factor) + cameraOffset;
    }

    // execute after physics simulation
    public void OtherInput(InputData inputData) {
        currTickPosition = movement.transform.position;

        if ((inputData.buttons & Buttons.Shoot) != 0) {
            weaponController.Shoot(inputData);
        }
    }

    public PlayerState GetState() {
        return new PlayerState {
            id = id,
            position = movement.transform.position,
            crouching = movement.IsCrouching()
        };
    }
}