using UnityEngine;
using UnityEngine.Serialization;

public class LocalPlayer : FixedBehaviour {
    public static LocalPlayer Instance;

    public PlayerMovement movement;
    public PlayerPrediction prediction;
    public PlayerInput playerInput;
    public WeaponController weapon;
    public PlayerCamera playerCamera;

    private void Awake() {
        if (Instance == null) {
            Instance = this;
        }
        else {
            Destroy(gameObject);
        }
    }

    private void Update() {
        playerInput.SampleInput();
    }

    public override void UpdateFixed() {
        uint tick = FixedClock.tick;

        InputData currentInputData = playerInput.GatherInput(tick);

        prediction.PredictState(currentInputData);

        if (currentInputData.shoot) {
            weapon.Shoot();
        }
        
        Physics.SyncTransforms();

        Physics.Simulate(NetworkSettings.tickTime);

        prediction.SaveState(tick);
    }
}