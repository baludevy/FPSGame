using System.Collections.Generic;
using UnityEngine;

public class SnapshotManager : MonoBehaviour {
    public static SnapshotManager Instance;

    public static uint serverTick;
    public static float clientRenderTick;
    public static uint snapshotBufferOffset;
    public static float interpTime => NetworkSettings.interpTime;

    private readonly List<WorldSnapshot> snapshotBuffer = new List<WorldSnapshot>();
    private readonly object bufferLock = new();
    private uint lastReconciledTick;
    private bool isInitialized;

    private void Awake() {
        Instance = this;
    }

    public void OnSnapshotReceived(WorldSnapshot snapshot) {
        if (snapshot == null) return;

        if (snapshot.serverTick > serverTick) {
            serverTick = snapshot.serverTick;
            snapshotBufferOffset = serverTick - (uint)Mathf.RoundToInt(clientRenderTick) - 1;

            float now = TickTimer.Instance.GetTime();
            ConnectionStatistics.CalculateStatistics(snapshot.serverTick, now, snapshot.clientSendTime,
                snapshot.serverReceiveTime, snapshot.serverSendTime);

            // adjust thresholds and target offset based on network conditions, sync clock to be ahead of the server
            ConnectionStatistics.ApplyAdjustments();
            TimeScaler.Instance.AdjustClock(snapshot.inputBufferOffset);
        }

        if (snapshot.serverTick > lastReconciledTick) {
            lastReconciledTick = snapshot.serverTick;
            int myId = Client.Instance.myId;
            foreach (PlayerState state in snapshot.playerStates) {
                if (state.id != myId) continue;
                if (PlayerMovement.Instance != null)
                    ThreadManager.ExecuteOnMainThread(() =>
                        PlayerPrediction.CompareServerState(state, snapshot.serverTick));
                break;
            }
        }

        lock (bufferLock) {
            AddToBuffer(snapshot);
        }
    }

    private void AddToBuffer(WorldSnapshot snapshot) {
        if (snapshotBuffer.Count == 0) {
            snapshotBuffer.Add(snapshot);
            return;
        }

        uint newest = snapshotBuffer[snapshotBuffer.Count - 1].serverTick;

        if (snapshot.serverTick > newest) {
            snapshotBuffer.Add(snapshot);
            return;
        }

        if (snapshot.serverTick <= snapshotBuffer[0].serverTick) return;

        for (int i = 0; i < snapshotBuffer.Count; i++) {
            if (snapshot.serverTick == snapshotBuffer[i].serverTick) return;
            if (snapshot.serverTick < snapshotBuffer[i].serverTick) {
                snapshotBuffer.Insert(i, snapshot);
                return;
            }
        }
    }

    private void LateUpdate() {
        lock (bufferLock) {
            if (snapshotBuffer.Count < 2) return;

            float tickRate = 1f / NetworkSettings.tickTime;
            float interpTicks = interpTime * tickRate;

            if (!isInitialized) {
                clientRenderTick = snapshotBuffer[snapshotBuffer.Count - 1].serverTick - interpTicks;
                isInitialized = true;
                return;
            }

            AdvanceRenderTick(tickRate, interpTicks);
            TrimBuffer();

            if (snapshotBuffer.Count < 2) return;

            GetInterpolationBounds(out WorldSnapshot from, out WorldSnapshot to);

            float tickDelta = to.serverTick - from.serverTick;
            float t = tickDelta > 0f ? (clientRenderTick - from.serverTick) / tickDelta : 0f;
            t = Mathf.Clamp01(t);

            ProcessSnapshot(from, to, t);
        }
    }

    private void AdvanceRenderTick(float tickRate, float interpTicks) {
        clientRenderTick += Time.deltaTime * tickRate;

        float targetTick = snapshotBuffer[snapshotBuffer.Count - 1].serverTick - interpTicks;
        float drift = clientRenderTick - targetTick;

        if (Mathf.Abs(drift) > interpTicks * 2f)
            clientRenderTick = targetTick;
        else
            clientRenderTick = Mathf.MoveTowards(clientRenderTick, targetTick, Time.deltaTime * tickRate * 0.1f);
    }

    private void TrimBuffer() {
        while (snapshotBuffer.Count > 2 && snapshotBuffer[1].serverTick < clientRenderTick)
            snapshotBuffer.RemoveAt(0);
    }

    private void GetInterpolationBounds(out WorldSnapshot from, out WorldSnapshot to) {
        for (int i = 0; i < snapshotBuffer.Count - 1; i++) {
            if (clientRenderTick >= snapshotBuffer[i].serverTick &&
                clientRenderTick <= snapshotBuffer[i + 1].serverTick) {
                from = snapshotBuffer[i];
                to = snapshotBuffer[i + 1];
                return;
            }
        }

        if (clientRenderTick < snapshotBuffer[0].serverTick) {
            from = snapshotBuffer[0];
            to = snapshotBuffer[0];
        }
        else {
            from = snapshotBuffer[snapshotBuffer.Count - 2];
            to = snapshotBuffer[snapshotBuffer.Count - 1];
        }
    }

    private void ProcessSnapshot(WorldSnapshot from, WorldSnapshot to, float t) {
        int myId = Client.Instance.myId;

        foreach (PlayerState toState in to.playerStates) {
            if (toState.id == myId) continue;
            if (!GameManager.players.TryGetValue(toState.id, out PlayerManager player) || player == null) continue;

            Vector3 fromPos = toState.position;
            for (int i = 0; i < from.playerStates.Count; i++) {
                if (from.playerStates[i].id == toState.id) {
                    fromPos = from.playerStates[i].position;
                    break;
                }
            }

            player.transform.position = Vector3.Lerp(fromPos, toState.position, t);
        }
    }
}