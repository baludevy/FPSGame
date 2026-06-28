using UnityEngine;

public class PlayerPrediction : MonoBehaviour {
    private const float positionErrorThreshold = 0.01f;

    private static Vector3[] positionHistory = new Vector3[NetworkSettings.inputHistorySize];
    private static Vector3[] velocityHistory = new Vector3[NetworkSettings.inputHistorySize];
    private Vector3 lastPredictedPos;
    [SerializeField] public Transform visualPlayer;

    [SerializeField] private float errorDecay = 12f;
    private Vector3 visualError;

    private float interpolationFactor;

    public void Interpolate() {
        if (visualPlayer == null)
            return;

        interpolationFactor = FixedClock.GetAccumulatedTime() / NetworkSettings.tickTime;
        interpolationFactor = Mathf.Clamp01(interpolationFactor);

        Vector3 interpolatedPos = Vector3.Lerp(
            lastPredictedPos,
            LocalPlayer.Instance.movement.transform.position,
            interpolationFactor
        );

        visualError = Vector3.Lerp(
            visualError,
            Vector3.zero,
            1f - Mathf.Exp(-errorDecay * Time.deltaTime)
        );

        visualPlayer.position = interpolatedPos + visualError;
        visualPlayer.rotation = transform.rotation;
    }

    public void PredictState(InputData inputData) {
        lastPredictedPos = LocalPlayer.Instance.movement.transform.position;

        LocalPlayer.Instance.movement.SetInput(inputData);
        LocalPlayer.Instance.movement.AdvanceLogic();
    }

    public void SaveState(uint tick) {
        uint i = tick % NetworkSettings.inputHistorySize;

        positionHistory[i] = LocalPlayer.Instance.movement.transform.position;
        velocityHistory[i] = LocalPlayer.Instance.movement.GetRb().velocity;
    }

    public static void CompareServerState(MovementState serverState, uint tick) {
        uint index = tick % NetworkSettings.inputHistorySize;

        Vector3 prePosition = positionHistory[index];

        float errorSqrMag = (serverState.position - prePosition).sqrMagnitude;
        if (errorSqrMag > positionErrorThreshold) {
            Debug.Log($"Desync by {errorSqrMag}, tick: {tick}");
            SynchronizeMovement(serverState, tick);
        }
    }

    private static void SynchronizeMovement(MovementState serverState, uint tick) {
        Vector3 preReconcileVisual = Vector3.zero;

        preReconcileVisual = LocalPlayer.Instance.prediction.visualPlayer.position;

        LocalPlayer.Instance.movement.transform.position = serverState.position;
        LocalPlayer.Instance.movement.GetRb().velocity = serverState.velocity;
        LocalPlayer.Instance.movement.GetOrientation().rotation =
            Quaternion.Euler(0f, serverState.orientation, 0f);

        uint lastSimulatedTick = FixedClock.tick - 1;

        for (uint i = tick + 1; i <= lastSimulatedTick; i++) {
            uint index = i % NetworkSettings.inputHistorySize;
            InputData inputData = PlayerInput.inputHistory[index];
            if (inputData == null) {
                Debug.LogWarning($"Missing input during replay at tick {i}, reusing last known input");
            }
            else {
                LocalPlayer.Instance.movement.SetInput(inputData);
            }

            LocalPlayer.Instance.movement.AdvanceLogic();
            Physics.Simulate(NetworkSettings.tickTime);

            positionHistory[index] = LocalPlayer.Instance.movement.transform.position;
            velocityHistory[index] = LocalPlayer.Instance.movement.GetRb().velocity;
        }

        LocalPlayer.Instance.prediction.lastPredictedPos = LocalPlayer.Instance.movement.transform.position;
        LocalPlayer.Instance.prediction.visualError =
            preReconcileVisual - LocalPlayer.Instance.movement.transform.position;
    }

    public float GetInterpolationFactor() {
        return interpolationFactor;
    }
}