using UnityEngine;

public class PlayerPrediction : MonoBehaviour {
    [SerializeField] public Transform visualPlayer;
    [SerializeField] private float errorDecay = 12f;

    private float positionErrorThreshold = 0.0001f;

    private static Vector3[] positionHistory = new Vector3[NetworkSettings.inputHistorySize];
    private static Vector3[] velocityHistory = new Vector3[NetworkSettings.inputHistorySize];

    private Vector3 visualError;
    private Vector3 lastPredictedPos;

    private float interpolationFactor;

    private Vector3 prevTickPos;
    private Vector3 currTickPos;

    public void Interpolate() {
        if (visualPlayer == null)
            return;

        interpolationFactor = FixedClock.GetInterpolationAlpha();
        interpolationFactor = Mathf.Clamp01(interpolationFactor);

        Vector3 interpolatedPos = Vector3.Lerp(
            prevTickPos,
            currTickPos,
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
    
    public void CapturePreTickState() {
        prevTickPos = Player.Instance.movement.transform.position; 
    }

    public void CapturePostTickState() {
        currTickPos = Player.Instance.movement.transform.position; 
    }

    public void PredictState(InputData inputData) {
        lastPredictedPos = transform.position;

        Player.Instance.movement.SetInput(inputData);
        Player.Instance.movement.AdvanceLogic();
    }

    public void SaveState(uint tick) {
        uint i = tick % NetworkSettings.inputHistorySize;

        positionHistory[i] = currTickPos;
        velocityHistory[i] = Player.Instance.movement.GetRb().linearVelocity;
    }

    public void CompareServerState(MovementState serverState, uint tick) {
        uint index = tick % NetworkSettings.inputHistorySize;

        Vector3 prePosition = positionHistory[index];

        float errorSqrMag = (serverState.position - prePosition).sqrMagnitude;
        if (errorSqrMag > positionErrorThreshold) {
            SynchronizeMovement(serverState, tick);
        }
    }

    private void SynchronizeMovement(MovementState serverState, uint tick) {
        Vector3 preReconcileVisual;

        preReconcileVisual = visualPlayer.position;

        Player.Instance.movement.transform.position = serverState.position;
        Player.Instance.movement.GetRb().linearVelocity = serverState.velocity;
        Player.Instance.movement.GetOrientation().rotation =
            Quaternion.Euler(0f, serverState.orientation, 0f);

        uint lastSimulatedTick = FixedClock.tick - 1;

        if (lastSimulatedTick < tick || lastSimulatedTick - tick >= NetworkSettings.inputHistorySize) {
            lastPredictedPos = Player.Instance.movement.transform.position;
            
            prevTickPos = Player.Instance.movement.transform.position;
            currTickPos = Player.Instance.movement.transform.position;
            
            visualError = preReconcileVisual - Player.Instance.movement.transform.position;
            return;
        }

        Vector3 replayPreTickPos = Player.Instance.movement.transform.position;

        for (uint i = tick + 1; i <= lastSimulatedTick; i++) {
            uint index = i % NetworkSettings.inputHistorySize;
            InputData inputData = PlayerInput.GetInputHistory()[index];
            if (inputData.tick != i) {
                // Debug.LogWarning($"Missing input during replay at tick {i}, reusing last known input");
            }
            else {
                Player.Instance.movement.SetInput(inputData);
            }
            
            if (i == lastSimulatedTick) {
                replayPreTickPos = Player.Instance.movement.transform.position;
            }

            Player.Instance.movement.AdvanceLogic();
            Physics.Simulate(NetworkSettings.tickTime);

            positionHistory[index] = Player.Instance.movement.transform.position;
            velocityHistory[index] = Player.Instance.movement.GetRb().linearVelocity;
        }
        
        prevTickPos = replayPreTickPos;
        currTickPos = Player.Instance.movement.transform.position;

        lastPredictedPos = Player.Instance.movement.transform.position;
        
        interpolationFactor = Mathf.Clamp01(FixedClock.GetInterpolationAlpha());
        Vector3 accurateTargetPos = Vector3.Lerp(prevTickPos, currTickPos, interpolationFactor);
        
        visualError = preReconcileVisual - accurateTargetPos;
    }
}