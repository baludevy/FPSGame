using System.Collections.Generic;
using UnityEngine;

public class SnapshotManager : MonoBehaviour {
    public static SnapshotManager Instance;

    private List<WorldSnapshot> snapshotBuffer = new List<WorldSnapshot>();

    private void Awake() {
        Instance = this;
    }

    public void OnSnapshotReceived(WorldSnapshot snapshot) {
        TimeScaler.Instance.AdjustClock(snapshot.inputBufferOffset);
        
        ConnectionStats.CalculateStats(snapshot);

        snapshotBuffer.Add(snapshot);
        snapshotBuffer.Sort((a, b) => {
            if (a == null || b == null) return 0;
            return a.serverTick.CompareTo(b.serverTick);
        });

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
                
                PlayerPrediction.CompareServerState(state, snapshot.serverTick);
                continue;
            }

            try {
                PlayerManager player = GameManager.players[state.id];

                if (player != null) player.transform.position = state.position;
            }
            catch {
                
            }
        }
    }
}