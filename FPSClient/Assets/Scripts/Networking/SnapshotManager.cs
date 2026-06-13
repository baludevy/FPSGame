using System.Collections.Generic;
using UnityEngine;

public class SnapshotManager : MonoBehaviour {
    public static SnapshotManager Instance;

    public float interpTime = 0.015625f; 

    private readonly List<WorldSnapshot> snapshotBuffer = new List<WorldSnapshot>();
    private float clientRenderTick;
    private bool isInitialized;

    private void Awake() {
        Instance = this;
    }

    public void OnSnapshotReceived(WorldSnapshot snapshot) {
        if (snapshot == null) return;

        TimeScaler.Instance.AdjustClock(snapshot.inputBufferOffset);
        ConnectionStats.CalculateStats(snapshot);

        if (snapshotBuffer.Count > 0 && snapshot.serverTick <= snapshotBuffer[snapshotBuffer.Count - 1].serverTick) {
            if (snapshot.serverTick <= snapshotBuffer[0].serverTick) return;

            int insertAt = snapshotBuffer.Count;
            for (int i = 0; i < snapshotBuffer.Count; i++) {
                if (snapshot.serverTick < snapshotBuffer[i].serverTick) {
                    insertAt = i;
                    break;
                }
            }
            snapshotBuffer.Insert(insertAt, snapshot);
        }
        else {
            snapshotBuffer.Add(snapshot);
        }
    }

    private void LateUpdate() {
        if (snapshotBuffer.Count == 0) return;

        float tickRate = 1f / NetworkSettings.tickTime;
        float interpTicks = interpTime * tickRate;

        if (!isInitialized) {
            clientRenderTick = snapshotBuffer[snapshotBuffer.Count - 1].serverTick - interpTicks;
            isInitialized = true;
            return;
        }

        clientRenderTick += Time.deltaTime * tickRate;

        float newestTick = snapshotBuffer[snapshotBuffer.Count - 1].serverTick;
        float targetTick = newestTick - interpTicks;
        float drift = clientRenderTick - targetTick;

        if (Mathf.Abs(drift) > interpTicks * 2f) {
            clientRenderTick = targetTick;
        }
        else {
            clientRenderTick = Mathf.MoveTowards(clientRenderTick, targetTick, Time.deltaTime * tickRate * 0.1f);
        }

        while (snapshotBuffer.Count > 2 && snapshotBuffer[1].serverTick < clientRenderTick) {
            snapshotBuffer.RemoveAt(0);
        }

        if (snapshotBuffer.Count < 2) return;

        WorldSnapshot fromSnap = snapshotBuffer[0];
        WorldSnapshot toSnap = snapshotBuffer[1];
        bool foundBounds = false;

        for (int i = 0; i < snapshotBuffer.Count - 1; i++) {
            if (clientRenderTick >= snapshotBuffer[i].serverTick && clientRenderTick <= snapshotBuffer[i + 1].serverTick) {
                fromSnap = snapshotBuffer[i];
                toSnap = snapshotBuffer[i + 1];
                foundBounds = true;
                break;
            }
        }

        if (!foundBounds) {
            if (clientRenderTick < snapshotBuffer[0].serverTick) {
                fromSnap = snapshotBuffer[0];
                toSnap = snapshotBuffer[0];
            }
            else {
                fromSnap = snapshotBuffer[snapshotBuffer.Count - 2];
                toSnap = snapshotBuffer[snapshotBuffer.Count - 1];
            }
        }

        float tickDelta = toSnap.serverTick - fromSnap.serverTick;
        float t = tickDelta > 0f ? (clientRenderTick - fromSnap.serverTick) / tickDelta : 0f;
        t = Mathf.Clamp01(t);

        ProcessSnapshot(fromSnap, toSnap, t);
    }

    private void ProcessSnapshot(WorldSnapshot from, WorldSnapshot to, float t) {
        int myId = Client.Instance.myId;

        foreach (PlayerState toState in to.playerStates) {
            int playerId = toState.id;

            if (playerId == myId) {
                if (PlayerMovement.Instance != null) {
                    PlayerPrediction.CompareServerState(toState, to.serverTick);
                }
                continue;
            }

            if (!GameManager.players.TryGetValue(playerId, out PlayerManager player) || player == null) {
                continue;
            }

            Vector3 fromPos = toState.position;
            for (int i = 0; i < from.playerStates.Count; i++) {
                if (from.playerStates[i].id == playerId) {
                    fromPos = from.playerStates[i].position;
                    break;
                }
            }

            player.transform.position = Vector3.Lerp(fromPos, toState.position, t);
        }
    }
}