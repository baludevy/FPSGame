using UnityEngine;

public class LocalPlayer : FixedBehaviour {
    public static LocalPlayer Instance;

    public PlayerMovement movement;
    public PlayerPrediction prediction;
    public PlayerInput playerInput;
    public WeaponController weapon;
    public PlayerCamera playerCamera;

    private InputData currentInput;
    
    private Vector3 prevTickPosition;
    private Vector3 currTickPosition;

    private void Awake() {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public override void UpdateBeforeTick() {
        playerInput.SampleInput();
    }

    public override void UpdateFixed() {
        uint tick = FixedClock.tick;
        
        currentInput = playerInput.GatherInput(tick);
        playerInput.ConsumeInput();
        
        prediction.PredictState(currentInput);

        Physics.SyncTransforms();
        Physics.Simulate(NetworkSettings.tickTime);

        if ((currentInput.buttons & Buttons.Shoot) != 0) {
            weapon.Shoot();
        }

        prediction.SaveState(tick);
    }

    public override void UpdateAfterTick() {
        prediction.Interpolate();
        
        playerCamera.MoveCamera();

        // only send 1 input per frame
        if (FixedClock.tick - 1 > playerInput.lastSentTick &&
            NetworkManager.Instance.currentState == NetworkManager.State.connected)
            playerInput.SendPlayerInputs();
    }
}