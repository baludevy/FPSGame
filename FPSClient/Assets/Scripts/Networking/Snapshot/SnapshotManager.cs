using System;
using UnityEngine;

public class SnapshotManager : MonoBehaviour {
    public static SnapshotManager Instance;

    public static uint serverTick;
    public static float clientRenderTick => interpolator.renderTick;
    public static int snapshotBufferOffset;
    public static float interpTime => NetworkSettings.interpTime;

    private static SnapshotBuffer buffer = new();
    private static SnapshotInterpolator interpolator = new();
    private static object bufferLock = new();

    private uint lastProcessedTick;
    private uint lastReconciledTick;

    private void Awake() => Instance = this;

    public void Reset() {
        serverTick = 0;
        lastProcessedTick = 0;
        lastReconciledTick = 0;
        snapshotBufferOffset = 0;

        lock (bufferLock) {
            buffer.Clear();
            interpolator.Reset();
        }
    }

    public void OnUpdateReceived(GameUpdate update) {
        if (LocalPlayer.Instance == null) return;

        lock (bufferLock) {
            if (update.serverTick <= lastProcessedTick) return;
            lastProcessedTick = update.serverTick;
        }

        if (update.serverTick > serverTick) {
            serverTick = update.serverTick;
            snapshotBufferOffset = Math.Max(0, (int)serverTick - Mathf.RoundToInt(clientRenderTick) - 1);

            float now = FixedClock.GetTime();
            ConnectionStatistics.UpdateStatistics(
                update.serverTick, now,
                update.clientSendTime, update.serverReceiveTime, update.serverSendTime, update.serverInputJitter);

            ConnectionStatistics.ApplyAdjustments();

            TimeScaler.AdjustClock(update.serverReceiveMargin);
        }

        if (update.serverTick > lastReconciledTick) {
            lastReconciledTick = update.serverTick;

            if (LocalPlayer.Instance.movement != null)
                ThreadManager.ExecuteOnMainThread(() =>
                    PlayerPrediction.CompareServerState(update.movementState, update.serverTick));
        }

        lock (bufferLock) {
            buffer.Add(update.worldSnapshot);
        }
    }

    private void Update() {
        lock (bufferLock) {
            if (!interpolator.Advance(buffer, Time.deltaTime)) return;
            interpolator.ApplyToPlayers(buffer, Client.Instance.myId);
        }
    }
}