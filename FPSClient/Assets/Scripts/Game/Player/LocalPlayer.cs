using UnityEngine;

public class LocalPlayer : FixedBehaviour
{
    public static LocalPlayer Instance;

    public PlayerMovement movement;
    public PlayerPrediction prediction;
    public PlayerInput playerInput;
    public WeaponController weapon;
    public PlayerCamera playerCamera;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Update()
    {
        playerInput.SampleInput();
    }

    public override void UpdateFixed()
    {
        uint tick = FixedClock.tick;

        InputData currentInputData = playerInput.GatherInput(tick);
        playerInput.ConsumeInput();

        prediction.PredictState(currentInputData);

        if ((currentInputData.buttons & Buttons.Shoot) != 0)
            weapon.Shoot();

        Physics.SyncTransforms();
        Physics.Simulate(NetworkSettings.tickTime);

        prediction.SaveState(tick);
    }
}