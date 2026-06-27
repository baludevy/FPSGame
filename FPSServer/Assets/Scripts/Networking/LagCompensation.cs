using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class LagCompensation {
    private List<WorldSnapshot> snapshotHistory = new List<WorldSnapshot>();

    public void SaveSnapshot(WorldSnapshot snapshot) {
        snapshotHistory.Add(snapshot);
    }

    public PlayerState GetRewoundState(float renderTick, int playerId) {
        WorldSnapshot interpolated = GetRewoundSnapshot(renderTick);
        return interpolated.playerStates.FirstOrDefault(p => p.id == playerId);
    }

    public WorldSnapshot GetRewoundSnapshot(float renderTick) {
        int floorTick = Mathf.FloorToInt(renderTick);
        int ceilTick = Mathf.CeilToInt(renderTick);

        WorldSnapshot fromSnapshot = snapshotHistory.FirstOrDefault(s => s.tick == floorTick)
                                     ?? snapshotHistory.OrderBy(s => s.tick).First();

        WorldSnapshot toSnapshot = snapshotHistory.FirstOrDefault(s => s.tick == ceilTick)
                                   ?? snapshotHistory.OrderBy(s => s.tick).First();

        float t = renderTick - floorTick;

        return InterpolateSnapshot(fromSnapshot, toSnapshot, t);
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

        return interpolated;
    }

    public void Update() {
        while (snapshotHistory.Count > NetworkSettings.tickRate) {
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