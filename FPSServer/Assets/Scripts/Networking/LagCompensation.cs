using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class LagCompensation {
    private List<WorldSnapshot> snapshotHistory = new List<WorldSnapshot>();
    private uint maxLagCompensationTicks => NetworkSettings.maxLagCompensationTicks;

    public void SaveSnapshot(WorldSnapshot snapshot) {
        snapshotHistory.Add(snapshot);
    }

    public Vector3 GetRewoundPosition(float renderTick, int playerId) {
        WorldSnapshot fromSnapshot = snapshotHistory.First(s => s.tick == Mathf.FloorToInt(renderTick));
        WorldSnapshot toSnapshot = snapshotHistory.First(s => s.tick == Mathf.CeilToInt(renderTick));

        PlayerState fromState = fromSnapshot.playerStates.First(p => p.id == playerId);
        PlayerState toState = toSnapshot.playerStates.First(p => p.id == playerId);

        float t = renderTick - Mathf.FloorToInt(renderTick);

        return Vector3.Lerp(fromState.position, toState.position, t);
    }

    public void Update() {
        while (snapshotHistory.Count > maxLagCompensationTicks) {
            uint oldestTick = uint.MaxValue;

            foreach (WorldSnapshot snapshot in snapshotHistory) {
                if (snapshot.tick < oldestTick) {
                    oldestTick = snapshot.tick;
                }
            }

            snapshotHistory.RemoveAll(s => s.tick == oldestTick);
        }
    }
}