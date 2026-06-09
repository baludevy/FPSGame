using UnityEngine;

public class PlayerPrediction : MonoBehaviour {
    private const float positionErrorThreshold = 0.0000001f;
    public static PlayerPrediction Instance;

    private readonly bool[] hasPositionHistory = new bool[NetworkSettings.inputBufferSize];

    private readonly Vector3[] positionHistory = new Vector3[NetworkSettings.inputBufferSize];

    private void Awake() {
        Instance = this;
    }

    public void PredictState(PlayerInput input) {
        int i = input.tick % NetworkSettings.inputBufferSize;

        // Debug.Log($"Performing prediction on tick: {input.currentTick} with x:{input.x()} y:{input.y()}");

        PlayerMovement.Instance.SetInputs(input.x, input.y, input.jumping, input.crouching);
        PlayerMovement.Instance.AdvanceLogic();

        Physics.Simulate(NetworkSettings.tickTime);

        positionHistory[i] = PlayerMovement.Instance.transform.position;
        hasPositionHistory[i] = true;
    }

    public void CompareServerState(PlayerState playerState, int tick) {
        // if (PlayerMovement.Instance == null || tick > NetworkManager.inputTick) return;

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
        PlayerMovement.Instance.rb.position = playerState.position;
        PlayerMovement.Instance.rb.velocity = playerState.velocity;
        
        Physics.SyncTransforms(); 

        int lastSimulatedTick = TickTimer.tick - 1;
        
        for (int i = tick + 1; i <= lastSimulatedTick; i++) {
            int cacheIndex = i % NetworkSettings.inputBufferSize;
            PlayerInput input = SendInput.Instance.inputHistory[cacheIndex];

            if (input == null || input.tick != i) {
                Debug.Log("fuck");
                break;
            }

            PlayerMovement.Instance.SetInputs(input.x, input.y, input.jumping, input.crouching);
            PlayerMovement.Instance.AdvanceLogic();
            
            Physics.Simulate(NetworkSettings.tickTime);
            
            positionHistory[cacheIndex] = PlayerMovement.Instance.transform.position;
            hasPositionHistory[cacheIndex] = true;
        }
    }
}