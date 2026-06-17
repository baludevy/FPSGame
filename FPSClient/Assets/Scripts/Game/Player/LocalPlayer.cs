using UnityEngine;

public class LocalPlayer : FixedBehaviour {
    public static LocalPlayer Instance;

    public PlayerMovement movement;
    public PlayerPrediction prediction;
    public InputManager input;
    public WeaponController weapon;
    public MoveCamera moveCamera;

    private void Awake() {
        if (Instance == null) {
            Instance = this;
        }
        else {
            Destroy(gameObject);
        }
    }

    private void Update() {
        input.SampleInput();
    }

    public override void UpdateFixed() {
        uint tick = FixedClock.tick;

        PlayerInput currentInput = input.GatherInput(tick);

        prediction.PredictState(currentInput);

        if (currentInput.shoot) {
            weapon.Shoot();
        }

        Physics.Simulate(NetworkSettings.tickTime);

        prediction.SaveState(tick);
    }
}