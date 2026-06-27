using UnityEngine;

public class LocalPlayer : FixedBehaviour {
    public static LocalPlayer Instance;

    public PlayerMovement movement;
    public PlayerPrediction prediction;
    public PlayerInput playerInput;
    public WeaponController weapon;
    public PlayerCamera playerCamera;

    private InputData currentInput;
    private uint lastSimTick;

    private void Awake() {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public override void UpdateBeforeTick() {
        playerInput.SampleInput();
    }

    public override void UpdateFixed() {
        uint tick = FixedClock.tick;
        lastSimTick = tick;

        currentInput = playerInput.GatherInput(tick);
        playerInput.ConsumeInput();
        
        if ((currentInput.buttons & Buttons.Shoot) != 0) {
            weapon.Shoot();
        }

        prediction.PredictState(currentInput);

        Physics.SyncTransforms();
        Physics.Simulate(NetworkSettings.tickTime);
        
        prediction.SaveState(tick);
    }

    public override void UpdateAfterTick() {
        prediction.Interpolate();

        // only send 1 input per frame
        if (FixedClock.tick - 1 > playerInput.lastSentTick)
            playerInput.SendPlayerInputs();
    }
}