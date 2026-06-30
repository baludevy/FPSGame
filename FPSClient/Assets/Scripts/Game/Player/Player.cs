using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

public class Player : FixedBehaviour {
    public static Player Instance;

    public PlayerMovement movement;
    public PlayerPrediction prediction;
    public PlayerInput input;
    public PlayerCamera camera;

    private InputData currentInput;

    private void Awake() {
        if (Instance == null) {
            Instance = this;
        }
        else {
            Destroy(gameObject);
        }
    }

    public override void UpdateBeforeTick() {
        input.SampleInput();
        camera.MouseMovement();
    }

    public override void UpdateFixed() {
        uint tick = FixedClock.tick;

        currentInput = input.GatherInput(tick);
        input.ConsumeInput();

        prediction.PredictState(currentInput);

        prediction.CapturePreTickState();
        
        Physics.SyncTransforms();
        Physics.Simulate(NetworkSettings.tickTime);

        prediction.CapturePostTickState();
        
        prediction.SaveState(tick);
    }

    public override void UpdateAfterTick() {
        prediction.Interpolate();

        camera.MoveCamera();

        // only send 1 input per frame
        if (FixedClock.tick - 1 > input.GetLastSentTick() &&
            NetworkManager.Instance.currentState == NetworkManager.State.connected)
            input.SendPlayerInputs();
    }
}