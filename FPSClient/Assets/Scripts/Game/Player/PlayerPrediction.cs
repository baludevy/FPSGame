using UnityEngine;

public class PlayerPrediction : MonoBehaviour {
    private const float positionErrorThreshold = 0.000001f;

    private static Vector3[] positionHistory = new Vector3[NetworkSettings.inputHistorySize];
    private static Vector3[] velocityHistory = new Vector3[NetworkSettings.inputHistorySize];
    public Transform visualPlayer;
    private Vector3 lastPredictedPos;


    private void Update() {
        Interpolate();
    }

    private void Interpolate() {
        if (visualPlayer == null)
            return;

        float interpolationFactor = FixedClock.GetAccumulatedTime() / NetworkSettings.tickTime;
        interpolationFactor = Mathf.Clamp01(interpolationFactor);

        Vector3 interpolatedPos = Vector3.Lerp(
            lastPredictedPos,
            LocalPlayer.Instance.movement.transform.position,
            interpolationFactor
        );

        visualPlayer.position = interpolatedPos;
        visualPlayer.rotation = transform.rotation;
    }

    public void PredictState(PlayerInput input) {
        lastPredictedPos = LocalPlayer.Instance.movement.transform.position;

        LocalPlayer.Instance.movement.SetInput(input);
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
            // Debug.Log($"Desync by {errorSqrMag}, tick: {tick}");
            SynchronizeMovement(serverState, tick);
        }
    }

    private static void SynchronizeMovement(MovementState serverState, uint tick) {
        LocalPlayer.Instance.movement.transform.position = serverState.position;
        LocalPlayer.Instance.movement.GetRb().velocity = serverState.velocity;
        LocalPlayer.Instance.movement.GetOrientation().rotation =
            Quaternion.Euler(0f, serverState.orientation, 0f);

        uint lastSimulatedTick = FixedClock.tick - 1;
        
        for (uint i = tick + 1; i <= lastSimulatedTick; i++) {
            uint index = i % NetworkSettings.inputHistorySize;
            PlayerInput input = InputManager.inputHistory[index];

            if (input == null) {
                // Debug.Log("fuck");
                continue;
            }

            LocalPlayer.Instance.movement.SetInput(input);
            LocalPlayer.Instance.movement.AdvanceLogic();
            
            Physics.Simulate(NetworkSettings.tickTime);

            positionHistory[index] = LocalPlayer.Instance.movement.transform.position;
            velocityHistory[index] = LocalPlayer.Instance.movement.GetRb().velocity;
        }
    }
}