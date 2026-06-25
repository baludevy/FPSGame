using System;
using System.Collections.Generic;
using UnityEngine;

public class SnapshotManager : MonoBehaviour {
    public static SnapshotManager Instance;

    public static uint serverTick;

    public float targetMargin = 0.024f;
    public float marginFilterSmoothing = 0.0625f;

    private float sensitivity = 0.08f;
    private float catchUpGainMult = 2.0f;
    private float jitterDeadbandMult = 0.25f;

    private float maxSpeedUp = 0.1f;
    private float maxSlowDown = 0.03f;

    private float timescaleSmoothing = 0.15f;
    private int deadbandHysteresisFrames = 5;

    private uint lastProcessedTick;
    private uint lastReconciledTick;

    private List<WorldSnapshot> snapshotQueue = new List<WorldSnapshot>();

    private float renderTick;
    private float playbackTime;
    private float playbackTimeScale = 1.0f;
    private bool isPlaybackInitialized;

    public float currentMargin;

    private float currentDeadband;
    private float targetTimescale = 1f;
    private int framesInsideDeadband;
    

    private void Awake() {
        Instance = this;
    }

    public void Reset() {
        serverTick = 0;
        lastProcessedTick = 0;
        lastReconciledTick = 0;
        snapshotQueue.Clear();
        isPlaybackInitialized = false;
        playbackTimeScale = 1.0f;
        currentMargin = 0f;
        currentDeadband = 0f;
        targetTimescale = 1f;
        framesInsideDeadband = 0;
        renderTick = 0f;
        playbackTime = 0f;
    }

    public void OnUpdateReceived(GameUpdate update) {
        if (LocalPlayer.Instance == null) {
            return;
        }

        if (update.serverTick <= lastProcessedTick) {
            return;
        }

        lastProcessedTick = update.serverTick;

        if (update.worldSnapshot != null) {
            QueueSnapshot(update.worldSnapshot);
        }

        if (update.serverTick > serverTick) {
            float now = FixedClock.GetTime();
            NetStatisticsManager.UpdateStatistics(update.serverTick, now, update.timingInfo, update.upstreamStatistics);
            NetworkTuner.Apply();

            TimeScaler.AdjustInputClock(NetStatistics.inputMargin);
        }

        if (update.serverTick > lastReconciledTick) {
            lastReconciledTick = update.serverTick;

            if (LocalPlayer.Instance.movement != null) {
                ThreadManager.ExecuteOnMainThread(() =>
                    PlayerPrediction.CompareServerState(update.movementState, update.serverTick));
            }
        }
    }

    private void QueueSnapshot(WorldSnapshot newSnapshot) {
        newSnapshot.clientReceiveTime = FixedClock.GetTime();

        int insertIndex = snapshotQueue.FindIndex(s => s.serverTick > newSnapshot.serverTick);
        if (insertIndex == -1) {
            snapshotQueue.Add(newSnapshot);
        }
        else {
            snapshotQueue.Insert(insertIndex, newSnapshot);
        }

        if (!isPlaybackInitialized && snapshotQueue.Count >= 2) {
            uint newestTick = snapshotQueue[snapshotQueue.Count - 1].serverTick;
            float safeTargetMargin = Mathf.Max(targetMargin, NetworkSettings.tickTime);
            
            renderTick = newestTick - (safeTargetMargin / NetworkSettings.tickTime);
            float latestServerTime = snapshotQueue[snapshotQueue.Count - 1].serverSendTime;
            playbackTime = latestServerTime - safeTargetMargin;
            currentMargin = safeTargetMargin;
            isPlaybackInitialized = true;
        }
    }

    private void Update() {
        if (!isPlaybackInitialized) {
            return;
        }

        if (snapshotQueue.Count == 0) {
            return;
        }

        playbackTime += Time.deltaTime * playbackTimeScale;

        AdjustPlaybackTimeScale();
        InterpolateWorldState();
        PruneOldSnapshots();
    }

    private void AdjustPlaybackTimeScale() {
        if (snapshotQueue.Count == 0) {
            currentMargin = 0f;
            return;
        }

        float safeTargetMargin = Mathf.Max(targetMargin, NetworkSettings.tickTime);
        float latestServerTime = snapshotQueue[snapshotQueue.Count - 1].serverSendTime;
        float rawMargin = latestServerTime - playbackTime;

        currentMargin = Mathf.Lerp(currentMargin, rawMargin, marginFilterSmoothing);

        float deviation = currentMargin - safeTargetMargin;

        currentDeadband = Mathf.Clamp(NetStatistics.downstreamJitter * jitterDeadbandMult, 0.0005f, 0.025f);

        if (Mathf.Abs(deviation) <= currentDeadband) {
            framesInsideDeadband++;
        }
        else {
            framesInsideDeadband = 0;
        }

        if (framesInsideDeadband >= deadbandHysteresisFrames) {
            targetTimescale = 1f;
        }
        else if (Mathf.Abs(deviation) > currentDeadband) {
            float gain = sensitivity * (deviation < 0f ? catchUpGainMult : 1f);
            float pTerm = (deviation / NetworkSettings.tickTime) * gain;
            targetTimescale = Mathf.Clamp(1f + pTerm, 1f - maxSlowDown, 1f + maxSpeedUp);
        }

        playbackTimeScale = Mathf.Lerp(playbackTimeScale, targetTimescale, timescaleSmoothing);
    }

    private void InterpolateWorldState() {
        if (snapshotQueue.Count < 2) {
            return;
        }

        float newestTime = snapshotQueue[snapshotQueue.Count - 1].serverSendTime;
        float oldestTime = snapshotQueue[0].serverSendTime;

        float sampleTime = Mathf.Clamp(playbackTime, oldestTime, newestTime);

        WorldSnapshot left = null;
        WorldSnapshot right = null;

        for (int i = 0; i < snapshotQueue.Count - 1; i++) {
            if (snapshotQueue[i].serverSendTime <= sampleTime && snapshotQueue[i + 1].serverSendTime > sampleTime) {
                left = snapshotQueue[i];
                right = snapshotQueue[i + 1];
                break;
            }
        }

        if (left == null || right == null) {
            left = snapshotQueue[snapshotQueue.Count - 2];
            right = snapshotQueue[snapshotQueue.Count - 1];
        }

        float span = right.serverSendTime - left.serverSendTime;
        float alpha = span > 0f ? (sampleTime - left.serverSendTime) / span : 0f;
        alpha = Mathf.Clamp(alpha, 0f, 1f);

        serverTick = left.serverTick;
        
        float calculatedTickDelta = (float)(right.serverTick - left.serverTick);
        renderTick = left.serverTick + (alpha * calculatedTickDelta);

        ApplyInterpolatedState(left, right, alpha);
    }

    private void PruneOldSnapshots() {
        if (snapshotQueue.Count <= 2) {
            return;
        }

        while (snapshotQueue.Count > 2) {
            if (snapshotQueue[1].serverSendTime <= playbackTime) {
                snapshotQueue.RemoveAt(0);
            }
            else {
                break;
            }
        }
    }

    private void ApplyInterpolatedState(WorldSnapshot from, WorldSnapshot to, float alpha) {
        foreach (PlayerState startState in from.playerStates) {
            if (startState.id == Client.Instance.myId) {
                continue;
            }

            int endIndex = to.playerStates.FindIndex(p => p.id == startState.id);

            if (endIndex == -1) {
                if (GameManager.players.TryGetValue(startState.id, out PlayerManager staticPlayer)) {
                    if (staticPlayer != null && staticPlayer.transform != null) {
                        staticPlayer.transform.position = startState.position;
                    }
                }
                continue;
            }

            PlayerState endState = to.playerStates[endIndex];

            if (GameManager.players.TryGetValue(startState.id, out PlayerManager player)) {
                if (player != null && player.transform != null) {
                    Vector3 target = Vector3.Lerp(startState.position, endState.position, alpha);
                    player.transform.position = target;
                }
            }
        }
    }
}