using System;
using System.Collections.Generic;
using UnityEngine;

public class SnapshotManager : MonoBehaviour
{
    public static SnapshotManager Instance;
    public static uint serverTick;

    public float targetMargin = 0.024f;
    public float driftCorrectionGain = 0.1f;
    public float marginSmoothingFactor = 0.02f;

    private uint lastProcessedTick;
    private uint lastReconciledTick;

    private readonly object _lock = new object();
    private readonly List<WorldSnapshot> snapshotQueue = new List<WorldSnapshot>();
    private float lastArrivalTime;
    private uint lastArrivalTick;
    private bool hasArrival;

    public float renderTick;
    private bool isPlaybackInitialized;
    public float currentMargin;

    private float lastFrameTime;

    private void Awake()
    {
        Instance = this;
    }

    public void Reset()
    {
        serverTick = 0;
        lastProcessedTick = 0;
        lastReconciledTick = 0;
        isPlaybackInitialized = false;
        renderTick = 0f;
        currentMargin = 0f;
        lastFrameTime = 0;

        lock (_lock)
        {
            snapshotQueue.Clear();
            lastArrivalTime = 0;
            lastArrivalTick = 0;
            hasArrival = false;
        }
    }

    public void OnUpdateReceived(GameUpdate update)
    {
        if (LocalPlayer.Instance == null || update.serverTick <= lastProcessedTick)
        {
            return;
        }
        lastProcessedTick = update.serverTick;

        float now = FixedClock.GetTime();

        if (update.worldSnapshot != null)
        {
            update.worldSnapshot.clientReceiveTime = now;
            update.worldSnapshot.consumed = false;

            lock (_lock)
            {
                InsertSorted(update.worldSnapshot);

                if (!hasArrival || update.worldSnapshot.serverTick > lastArrivalTick)
                {
                    lastArrivalTime = now;
                    lastArrivalTick = update.worldSnapshot.serverTick;
                    hasArrival = true;
                }
            }
        }

        if (update.serverTick > serverTick)
        {
            serverTick = update.serverTick;
            NetStatisticsManager.UpdateStatistics(update.serverTick, now, update.timingInfo, update.upstreamStatistics);
            NetworkTuner.Apply();
            TimeScaler.AdjustInputClock(NetStatistics.inputMargin);
        }

        if (update.serverTick > lastReconciledTick)
        {
            lastReconciledTick = update.serverTick;
            if (LocalPlayer.Instance.movement != null)
            {
                ThreadManager.ExecuteOnMainThread(() =>
                {
                    PlayerPrediction.CompareServerState(update.movementState, update.serverTick);
                });
            }
        }
    }

    private void InsertSorted(WorldSnapshot snap)
    {
        int index = snapshotQueue.FindIndex(s => s.serverTick >= snap.serverTick);
        if (index == -1)
        {
            snapshotQueue.Add(snap);
        }
        else if (snapshotQueue[index].serverTick > snap.serverTick)
        {
            snapshotQueue.Insert(index, snap);
        }
    }
    
    private void Update()
    {
        WorldSnapshot[] snaps;
        float now = FixedClock.GetTime();

        lock (_lock)
        {
            if (!isPlaybackInitialized && snapshotQueue.Count >= 2 && hasArrival)
            {
                float interpTicksInit = Mathf.Max(targetMargin, NetworkSettings.tickTime) / NetworkSettings.tickTime;
                renderTick = (lastArrivalTick + (float)(now - lastArrivalTime) / NetworkSettings.tickTime) - interpTicksInit;
                lastFrameTime = now;
                isPlaybackInitialized = true;
            }

            if (!isPlaybackInitialized || snapshotQueue.Count < 2)
            {
                lastFrameTime = now;
                return;
            }

            snaps = snapshotQueue.ToArray();
        }

        AdvanceRenderTick(now, snaps[snaps.Length - 1].serverTick);
        InterpolateWorldState(snaps);
        PruneOldSnapshots();
    }
    
    private void AdvanceRenderTick(float now, uint newestQueuedTick)
    {
        float tickRate = 1f / NetworkSettings.tickTime;
        float deltaTime = Mathf.Clamp((now - lastFrameTime), 0f, 0.25f);
        lastFrameTime = now;
        
        float marginError = currentMargin - targetMargin;
        float playbackSpeedMultiplier = Mathf.Clamp(1f + (marginError / NetworkSettings.tickTime) * driftCorrectionGain, 0.9f, 1.1f);

        renderTick = Mathf.Min(renderTick + deltaTime * tickRate * playbackSpeedMultiplier, newestQueuedTick);
    }

    private void InterpolateWorldState(WorldSnapshot[] snaps)
    {
        WorldSnapshot left = snaps[snaps.Length - 2];
        WorldSnapshot right = snaps[snaps.Length - 1];

        for (int i = 0; i < snaps.Length - 1; i++)
        {
            if (renderTick >= snaps[i].serverTick && renderTick <= snaps[i + 1].serverTick)
            {
                left = snaps[i];
                right = snaps[i + 1];
                break;
            }
        }
        if (renderTick < snaps[0].serverTick)
        {
            left = snaps[0];
            right = snaps[0];
        }
        
        if (!left.consumed)
        {
            left.consumed = true;
            float consumptionDelta = Mathf.Max(0f, FixedClock.GetTime() - left.clientReceiveTime);
            currentMargin = currentMargin <= 0f ? consumptionDelta : Mathf.Lerp(currentMargin, consumptionDelta, marginSmoothingFactor);
        }

        float tickSpan = right.serverTick - left.serverTick;
        float alpha = tickSpan > 0f ? Mathf.Clamp01((renderTick - left.serverTick) / tickSpan) : 0f;

        serverTick = left.serverTick;
        ApplyInterpolatedState(left, right, alpha);
    }

    private void PruneOldSnapshots()
    {
        lock (_lock)
        {
            while (snapshotQueue.Count > 2 && snapshotQueue[1].serverTick < renderTick)
            {
                snapshotQueue.RemoveAt(0);
            }
        }
    }

    private void ApplyInterpolatedState(WorldSnapshot from, WorldSnapshot to, float alpha)
    {
        for (int i = 0; i < from.playerStates.Count; i++)
        {
            PlayerState startState = from.playerStates[i];
            if (startState.id == Client.Instance.myId) continue;

            int endIndex = to.playerStates.FindIndex(s => s.id == startState.id);
            if (GameManager.players.TryGetValue(startState.id, out PlayerManager player))
            {
                player.transform.position = endIndex == -1 ? startState.position : Vector3.Lerp(startState.position, to.playerStates[endIndex].position, alpha);
            }
        }
    }
}