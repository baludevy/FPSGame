using System.Collections.Concurrent;
using UnityEngine;

public class UpdateManager : MonoBehaviour {
    public static UpdateManager Instance;

    private static float targetMargin => NetcodeState.targetReceiveMargin;
    private const float driftCorrectionGain = 0.1f;
    private const float receiveMarginSmooth = 2f / (32f + 1f);

    public static uint serverTick;
    private static float renderTick;
    private static float currentMargin;

    private uint lastProcessedTick;
    private uint lastReconciledTick;

    private float lastArrivalTime;
    private uint lastArrivalTick;
    private bool hasArrival;

    private bool isInterpInitialized;
    private bool hasMargin;
    private float lastFrameTime;

    private const int MaxSnapshots = 64;
    private WorldSnapshot[] snapshots = new WorldSnapshot[MaxSnapshots];
    private int snapshotCount;

    private ConcurrentQueue<GameUpdate> incoming = new ConcurrentQueue<GameUpdate>();

    private void Awake() {
        Instance = this;
    }

    public void OnUpdateReceived(GameUpdate update) {
        incoming.Enqueue(update);
    }

    private void ProcessUpdate(GameUpdate update) {
        if (Player.Instance == null || update.serverTick <= lastProcessedTick) {
            return;
        }

        lastProcessedTick = update.serverTick;

        if (update.worldSnapshot.playerStates != null) {
            update.worldSnapshot.consumed = false;
            InsertSorted(update.worldSnapshot);

            if (!hasArrival || update.worldSnapshot.serverTick > lastArrivalTick) {
                lastArrivalTime = update.timingInfo.clientReceiveTime;
                lastArrivalTick = update.worldSnapshot.serverTick;
                hasArrival = true;
            }
        }

        if (update.serverTick > serverTick) {
            serverTick = update.serverTick;
        }

        if (update.serverTick > lastReconciledTick) {
            lastReconciledTick = update.serverTick;
            if (Player.Instance != null) {
                Player.Instance.prediction.CompareServerState(update.movementState, update.serverTick);
            }
        }
    }

    private void InsertSorted(WorldSnapshot snap) {
        if (snapshotCount >= MaxSnapshots) {
            for (int i = 1; i < snapshotCount; i++) snapshots[i - 1] = snapshots[i];
            snapshotCount--;
        }

        int idx = snapshotCount;
        while (idx > 0 && snapshots[idx - 1].serverTick > snap.serverTick) {
            snapshots[idx] = snapshots[idx - 1];
            idx--;
        }
        if (idx > 0 && snapshots[idx - 1].serverTick == snap.serverTick) {
            return;
        }
        snapshots[idx] = snap;
        snapshotCount++;
    }

    private void Update() {
        float now = FixedClock.GetTime();

        while (incoming.TryDequeue(out GameUpdate update)) {
            ProcessUpdate(update);
        }

        if (!isInterpInitialized && snapshotCount >= 2 && hasArrival) {
            float interpTicksInit = Mathf.Max(targetMargin, NetworkSettings.tickTime) / NetworkSettings.tickTime;
            renderTick = lastArrivalTick + (now - lastArrivalTime) / NetworkSettings.tickTime - interpTicksInit;
            lastFrameTime = now;
            isInterpInitialized = true;
        }

        if (!isInterpInitialized || snapshotCount < 2) {
            lastFrameTime = now;
            return;
        }

        AdvanceWorldState(now, snapshots[snapshotCount - 1].serverTick);
        InterpolateWorldState();
        PruneOldSnapshots();
    }

    private void AdvanceWorldState(float now, uint newestQueuedTick) {
        float tickRate = 1f / NetworkSettings.tickTime;
        float deltaTime = Mathf.Clamp(now - lastFrameTime, 0f, 0.25f);
        lastFrameTime = now;

        if (!hasMargin) {
            return;
        }

        if (currentMargin - targetMargin > 0.1f) {
            float excessTicks = (currentMargin - targetMargin) * tickRate;
            renderTick = Mathf.Min(renderTick + excessTicks, newestQueuedTick);
            return;
        }

        float marginError = currentMargin - targetMargin;
        float playbackSpeed = Mathf.Clamp(1f + marginError / NetworkSettings.tickTime * driftCorrectionGain, 0.9f, 1.1f);
        renderTick = Mathf.Min(renderTick + deltaTime * tickRate * playbackSpeed, newestQueuedTick);
    }

    private void InterpolateWorldState() {
        int leftIdx = 0;
        int rightIdx = 0;
        bool found = false;

        for (int i = 0; i < snapshotCount - 1; i++) {
            if (renderTick >= snapshots[i].serverTick && renderTick <= snapshots[i + 1].serverTick) {
                leftIdx = i;
                rightIdx = i + 1;
                found = true;
                break;
            }
        }

        if (!found) {
            if (renderTick < snapshots[0].serverTick) {
                leftIdx = 0;
                rightIdx = 0;
            }
            else {
                leftIdx = snapshotCount - 2;
                rightIdx = snapshotCount - 1;
                renderTick = snapshots[leftIdx].serverTick;
            }
        }

        WorldSnapshot left = snapshots[leftIdx];
        WorldSnapshot right = snapshots[rightIdx];

        if (!right.consumed) {
            right.consumed = true;
            snapshots[rightIdx] = right;
            float consumptionDelta = Mathf.Max(0f, FixedClock.GetTime() - right.clientReceiveTime);
            if (!hasMargin) {
                currentMargin = consumptionDelta;
                hasMargin = true;
            }
            else {
                currentMargin = receiveMarginSmooth * consumptionDelta + (1f - receiveMarginSmooth) * currentMargin;
            }
        }

        float tickSpan = right.serverTick - left.serverTick;
        float alpha = 0f;
        if (tickSpan > 0f) {
            float tickProgress = (renderTick - left.serverTick) / tickSpan;
            float timeSpan = right.serverSendTime - left.serverSendTime;
            float estimatedRenderTime = left.serverSendTime + (tickProgress * timeSpan);
            alpha = Mathf.Clamp01((estimatedRenderTime - left.serverSendTime) / timeSpan);
        }

        ApplyInterpolatedState(left, right, alpha);
    }

    private void PruneOldSnapshots() {
        while (snapshotCount > 2 && snapshots[1].serverTick < renderTick) {
            for (int i = 1; i < snapshotCount; i++) snapshots[i - 1] = snapshots[i];
            snapshotCount--;
        }
    }

    private void ApplyInterpolatedState(WorldSnapshot from, WorldSnapshot to, float alpha) {
        int fromCount = from.playerStatesCount;
        int toCount = to.playerStatesCount;

        for (int i = 0; i < fromCount; i++) {
            PlayerState startState = from.playerStates[i];
            if (startState.id == Client.Instance.myId) {
                continue;
            }

            int endIndex = -1;
            for (int j = 0; j < toCount; j++) {
                if (to.playerStates[j].id == startState.id) {
                    endIndex = j;
                    break;
                }
            }

            if (GameManager.players.TryGetValue(startState.id, out PlayerManager player)) {
                if (endIndex == -1) {
                    player.transform.position = startState.position;
                }
                else {
                    player.transform.position = Vector3.Lerp(startState.position, to.playerStates[endIndex].position, alpha);
                }
            }
        }
    }

    public static float GetCurrentReceiveMargin() {
        return currentMargin;
    }

    public static float GetRenderTick() {
        return renderTick;
    }

    public void Reset() {
        serverTick = 0;
        lastProcessedTick = 0;
        lastReconciledTick = 0;
        isInterpInitialized = false;
        renderTick = 0f;
        currentMargin = 0f;
        hasMargin = false;
        lastFrameTime = 0;

        while (incoming.TryDequeue(out GameUpdate update)) { }

        snapshotCount = 0;
        lastArrivalTime = 0;
        lastArrivalTick = 0;
        hasArrival = false;
    }
}