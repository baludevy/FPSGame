using UnityEngine;
using UnityEngine.Serialization;

public class PlayerPrediction : MonoBehaviour {
    private const float positionErrorThreshold = 0.0000001f;
    public static PlayerPrediction Instance;

    private readonly bool[] hasPositionHistory = new bool[NetworkSettings.inputBufferSize];
    private readonly Vector3[] positionHistory = new Vector3[NetworkSettings.inputBufferSize];
    
    public Transform visualPlayer;
    private Vector3 lastPredictedPos;
    private Vector3 currentPredictedPos;
    
    private Vector3 visualOffset = Vector3.zero;
    public float visualCatchupSpeed = 15f;

    private void Awake() {
        Instance = this;
    }

    private void Start() {
        if (visualPlayer != null) {
            lastPredictedPos = transform.position;
            currentPredictedPos = transform.position;
            visualPlayer.position = transform.position;
        }
    }
    
    private void Update()
    {
        if (visualPlayer == null)
            return;
        
        if (visualOffset.sqrMagnitude > 0.0001f) {
            visualOffset = Vector3.Lerp(visualOffset, Vector3.zero, Time.deltaTime * visualCatchupSpeed);
        } else {
            visualOffset = Vector3.zero;
        }
        
        float interpolationFactor = (float)TickTimer.Instance.accumulator / NetworkSettings.tickTime;
        interpolationFactor = Mathf.Clamp01(interpolationFactor);
        
        Vector3 interpolatedPos = Vector3.Lerp(
            lastPredictedPos,
            currentPredictedPos,
            interpolationFactor
        );
        
        visualPlayer.position = interpolatedPos + visualOffset;
        visualPlayer.rotation = transform.rotation;
    }

    public void PredictState(PlayerInput input) {
        int i = input.tick % NetworkSettings.inputBufferSize;

        lastPredictedPos = currentPredictedPos;

        PlayerMovement.Instance.SetInputs(input.x, input.y, input.jumping, input.crouching);
        PlayerMovement.Instance.AdvanceLogic();

        Physics.Simulate(NetworkSettings.tickTime);

        positionHistory[i] = PlayerMovement.Instance.transform.position;
        hasPositionHistory[i] = true;

        currentPredictedPos = PlayerMovement.Instance.transform.position;
    }

    public void CompareServerState(PlayerState playerState, int tick) {
        int index = tick % NetworkSettings.inputBufferSize;

        if (!hasPositionHistory[index]) return;

        Vector3 prePosition = positionHistory[index];

        float errorSqrMag = (playerState.position - prePosition).sqrMagnitude;
        if (errorSqrMag > positionErrorThreshold) {
            Debug.Log($"Desync by {errorSqrMag}");
            SynchronizeMovement(playerState, tick);
        }
    }

    private void SynchronizeMovement(PlayerState playerState, int tick) {
        Vector3 predictedPosBeforeSync = currentPredictedPos;

        PlayerMovement.Instance.rb.position = playerState.position;
        PlayerMovement.Instance.rb.velocity = playerState.velocity;
        
        Physics.SyncTransforms(); 

        int lastSimulatedTick = TickTimer.tick - 1;
        
        for (int i = tick + 1; i <= lastSimulatedTick; i++) {
            int cacheIndex = i % NetworkSettings.inputBufferSize;
            PlayerInput input = SendInput.Instance.inputHistory[cacheIndex];

            if (input == null || input.tick != i) {
                Debug.LogWarning("Missing input history during reconciliation!");
                break;
            }

            PlayerMovement.Instance.SetInputs(input.x, input.y, input.jumping, input.crouching);
            PlayerMovement.Instance.AdvanceLogic();
            
            Physics.Simulate(NetworkSettings.tickTime);
            
            positionHistory[cacheIndex] = PlayerMovement.Instance.transform.position;
            hasPositionHistory[cacheIndex] = true;
        }
        
        currentPredictedPos = PlayerMovement.Instance.transform.position;
        
        Vector3 correctionDelta = currentPredictedPos - predictedPosBeforeSync;
        
        lastPredictedPos += correctionDelta;
        
        visualOffset -= correctionDelta;
    }
}