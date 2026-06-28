using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class LagCompensation {
    private List<WorldSnapshot> snapshotHistory = new List<WorldSnapshot>();

    public void SaveSnapshot(WorldSnapshot snapshot) {
        int index = snapshotHistory.FindIndex(s => s.tick >= snapshot.tick);
        if (index == -1) {
            snapshotHistory.Add(snapshot);
        }
        else if (snapshotHistory[index].tick == snapshot.tick) {
            snapshotHistory[index] = snapshot;
        }
        else {
            snapshotHistory.Insert(index, snapshot);
        }
    }

    public PlayerState GetRewoundState(float renderTick, int playerId) {
        WorldSnapshot interpolated = GetRewoundSnapshot(renderTick);
        if (interpolated == null) {
            return null;
        }
        return interpolated.playerStates.FirstOrDefault(p => p.id == playerId);
    }

    public WorldSnapshot GetRewoundSnapshot(float renderTick) {
        if (snapshotHistory.Count == 0) {
            return null;
        }

        uint oldestTick = snapshotHistory[0].tick;
        uint newestTick = snapshotHistory[snapshotHistory.Count - 1].tick;

        float clampedTick = Mathf.Clamp(renderTick, oldestTick, newestTick);

        if (snapshotHistory.Count == 1) {
            return InterpolateSnapshot(snapshotHistory[0], snapshotHistory[0], 0f);
        }

        WorldSnapshot from = snapshotHistory[0];
        WorldSnapshot to = snapshotHistory[snapshotHistory.Count - 1];

        for (int i = 0; i < snapshotHistory.Count - 1; i++) {
            if (clampedTick >= snapshotHistory[i].tick && clampedTick <= snapshotHistory[i + 1].tick) {
                from = snapshotHistory[i];
                to = snapshotHistory[i + 1];
                break;
            }
        }

        float span = to.tick - from.tick;
        float t = span > 0f ? Mathf.Clamp01((clampedTick - from.tick) / span) : 0f;

        return InterpolateSnapshot(from, to, t);
    }

    private WorldSnapshot InterpolateSnapshot(WorldSnapshot from, WorldSnapshot to, float t) {
        WorldSnapshot interpolated = new WorldSnapshot {
            tick = from.tick,
            playerStates = new List<PlayerState>()
        };

        foreach (PlayerState fromState in from.playerStates) {
            PlayerState toState = to.playerStates.FirstOrDefault(p => p.id == fromState.id);

            PlayerState interpolatedState = new PlayerState {
                id = fromState.id,
                position = toState != null
                    ? Vector3.Lerp(fromState.position, toState.position, t)
                    : fromState.position
            };

            interpolated.playerStates.Add(interpolatedState);
        }

        foreach (PlayerState toState in to.playerStates) {
            if (interpolated.playerStates.All(p => p.id != toState.id)) {
                interpolated.playerStates.Add(new PlayerState {
                    id = toState.id,
                    position = toState.position
                });
            }
        }

        return interpolated;
    }

    public void Update() {
        int maxSnapshots = (int)NetworkSettings.tickRate;
        if (snapshotHistory.Count > maxSnapshots) {
            snapshotHistory.RemoveRange(0, snapshotHistory.Count - maxSnapshots);
        }
    }
}