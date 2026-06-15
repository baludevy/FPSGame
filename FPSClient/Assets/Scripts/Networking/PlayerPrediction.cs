using UnityEngine;

public class PlayerPrediction : MonoBehaviour {
    private const float positionErrorThreshold = 0.0000001f;
    public static PlayerPrediction Instance;

    private static Vector3[] positionHistory = new Vector3[NetworkSettings.inputBufferSize];
    private static Vector3[] velocityHistory = new Vector3[NetworkSettings.inputBufferSize];
    public Transform visualPlayer;
    private Vector3 lastPredictedPos;

    private void Awake() {
        Instance = this;
    }

    private void Update() {
        if (visualPlayer == null)
            return;

        float interpolationFactor = (float)TickTimer.Instance.accumulator / NetworkSettings.tickTime;
        interpolationFactor = Mathf.Clamp01(interpolationFactor);

        Vector3 interpolatedPos = Vector3.Lerp(
            lastPredictedPos,
            PlayerMovement.Instance.transform.position,
            interpolationFactor
        );

        visualPlayer.position = interpolatedPos;
        visualPlayer.rotation = transform.rotation;
    }

    public void PredictState(PlayerInput input) {
        uint i = input.tick % NetworkSettings.inputBufferSize;

        lastPredictedPos = PlayerMovement.Instance.transform.position;

        PlayerMovement.Instance.SetInput(
            input.x,
            input.y,
            input.orientation,
            input.jumping,
            input.crouching
        );

        PlayerMovement.Instance.AdvanceLogic();

        Physics.SyncTransforms();

        Physics.Simulate(NetworkSettings.tickTime);

        positionHistory[i] = PlayerMovement.Instance.transform.position;
        velocityHistory[i] = PlayerMovement.Instance.rb.velocity;
    }

    public static void CompareServerState(MovementState serverState, uint tick) {
        uint index = tick % NetworkSettings.inputBufferSize;

        Vector3 prePosition = positionHistory[index];

        float errorSqrMag = (serverState.position - prePosition).sqrMagnitude;
        if (errorSqrMag > positionErrorThreshold) {
            Debug.Log($"Desync by {errorSqrMag}, tick: {tick}");
            SynchronizeMovement(serverState, tick);
        }
    }

    private static void SynchronizeMovement(MovementState serverState, uint tick) {
        Vector3 prePosition = PlayerMovement.Instance.transform.position;

        PlayerMovement.Instance.transform.position = serverState.position;
        PlayerMovement.Instance.rb.velocity = serverState.velocity;
        PlayerMovement.Instance.orientation.rotation =
            Quaternion.Euler(0f, serverState.orientation, 0f);

        uint lastSimulatedTick = TickTimer.tick - 1;

        MoveCamera.Instance.smooth = true;

        for (uint i = tick + 1; i <= lastSimulatedTick; i++) {
            uint index = i % NetworkSettings.inputBufferSize;
            PlayerInput input = SendInput.Instance.inputHistory[index];

            if (input == null) {
                Debug.Log("fuck");
                continue;
            }

            PlayerMovement.Instance.SetInput(input.x, input.y, input.orientation, input.jumping, input.crouching);

            PlayerMovement.Instance.AdvanceLogic();

            Physics.SyncTransforms();
            Physics.Simulate(NetworkSettings.tickTime);

            positionHistory[index] = PlayerMovement.Instance.transform.position;
            velocityHistory[index] = PlayerMovement.Instance.rb.velocity;
        }

        Vector3 offsetPosition = PlayerMovement.Instance.transform.position - prePosition;
        MoveCamera.Instance.desyncOffset = -offsetPosition;

        MoveCamera.Instance.smooth = false;
    }
}