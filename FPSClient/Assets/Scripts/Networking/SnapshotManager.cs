using System.Collections.Generic;
using UnityEngine;

public class SnapshotManager : MonoBehaviour {
    public static SnapshotManager Instance;

    private List<WorldSnapshot> snapshotBuffer = new List<WorldSnapshot>();

    private void Awake() {
        Instance = this;
    }

    public void AddSnapshot(WorldSnapshot snapshot) {
        TimeScaler.Instance.AdjustClock(snapshot.bufferSlack);
        
        snapshotBuffer.Add(snapshot);
        snapshotBuffer.Sort((a, b) => a.serverTick.CompareTo(b.serverTick));

        if (snapshotBuffer.Count > 30) {
            snapshotBuffer.RemoveAt(0);
        }
    }

    private void Update() {
        if (snapshotBuffer.Count == 0) return;

        WorldSnapshot latestSnapshot = snapshotBuffer[snapshotBuffer.Count - 1];
        ProcessSnapshot(latestSnapshot);

        snapshotBuffer.Clear();
    }

    private void ProcessSnapshot(WorldSnapshot snapshot) {
        foreach (PlayerState state in snapshot.playerStates) {
            if (PlayerMovement.Instance != null && state.id == Client.Instance.myId) {
                // Debug.Log($"latest snapshot position: {state.position}, local: {PlayerMovement.Instance.transform.position}, was {TickTimer.tick - snapshot.serverTick} ticks ago");
                
                PlayerPrediction.Instance.CompareServerState(state, snapshot.serverTick);
                continue;
            }

            GameManager.players[state.id].transform.position = state.position;
        }
    }
}