using System;
using System.Collections.Generic;
using UnityEngine;

public class SnapshotManager : MonoBehaviour {
    public static SnapshotManager Instance;

    public static uint serverTick;
    public static float interpTime => NetworkSettings.interpTime;

    private uint lastProcessedTick;
    private uint lastReconciledTick;

    private void Awake() {
        Instance = this;
    }

    public void Reset() {
        serverTick = 0;
        lastProcessedTick = 0;
        lastReconciledTick = 0;
    }

    public void OnUpdateReceived(GameUpdate update) {
        if (LocalPlayer.Instance == null) return;

        if (update.serverTick <= lastProcessedTick) return;
        lastProcessedTick = update.serverTick;

        if (update.serverTick > serverTick) {
            serverTick = update.serverTick;

            float now = FixedClock.GetTime();
            NetStatisticsManager.UpdateStatistics(update.serverTick, now, update.timingInfo, update.upstreamStatistics);

            NetStatisticsManager.ApplyAdjustments();

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
}