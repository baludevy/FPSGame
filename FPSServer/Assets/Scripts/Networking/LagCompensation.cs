using UnityEngine;

public class LagCompensation {
    private WorldSnapshot[] history;
    private int capacity;
    private int count;
    private int head;

    public LagCompensation() {
        capacity = NetworkSettings.tickRate;
        
        history = new WorldSnapshot[capacity];
        for (int i = 0; i < capacity; i++) {
            history[i].playerStates = new PlayerState[Server.maxPlayers];
        }
        
        count = 0;
        head = 0;
    }

    public void SaveSnapshot(WorldSnapshot snapshot) {
        int slot = head;
        PlayerState[] dest = history[slot].playerStates;

        int n = snapshot.playerStatesCount;
        for (int i = 0; i < n; i++) {
            dest[i] = snapshot.playerStates[i];
        }

        history[slot].serverTick = snapshot.serverTick;
        history[slot].serverSendTime = snapshot.serverSendTime;
        history[slot].playerStatesCount = n;
        history[slot].playerStates = dest;

        head = (head + 1) % capacity;
        if (count < capacity) {
            count++;
        }
    }

    private int OldestIndex() {
        if (count < capacity) {
            return 0;
        }
        return head;
    }

    private int IndexAt(int offset) {
        return (OldestIndex() + offset) % capacity;
    }

    public bool GetRewoundState(float renderTick, int playerId, out PlayerState result) {
        result = default;

        if (count == 0) {
            return false;
        }

        if (!GetRewoundSnapshot(renderTick, out WorldSnapshot interpolated)) {
            return false;
        }

        for (int i = 0; i < interpolated.playerStatesCount; i++) {
            if (interpolated.playerStates[i].id == playerId) {
                result = interpolated.playerStates[i];
                return true;
            }
        }
        return false;
    }

    private PlayerState[] scratch = new PlayerState[Server.maxPlayers];

    public bool GetRewoundSnapshot(float renderTick, out WorldSnapshot result) {
        result = default;

        if (count == 0) {
            return false;
        }

        uint oldestTick = history[IndexAt(0)].serverTick;
        uint newestTick = history[IndexAt(count - 1)].serverTick;

        float clampedTick = Mathf.Clamp(renderTick, oldestTick, newestTick);

        if (count == 1) {
            result = history[IndexAt(0)];
            return true;
        }

        int fromIdx = IndexAt(0);
        int toIdx = IndexAt(count - 1);

        for (int i = 0; i < count - 1; i++) {
            int a = IndexAt(i);
            int b = IndexAt(i + 1);
            if (clampedTick >= history[a].serverTick && clampedTick <= history[b].serverTick) {
                fromIdx = a;
                toIdx = b;
                break;
            }
        }

        WorldSnapshot from = history[fromIdx];
        WorldSnapshot to = history[toIdx];

        float span = to.serverTick - from.serverTick;
        float t = span > 0f ? Mathf.Clamp01((clampedTick - from.serverTick) / span) : 0f;

        result = InterpolateSnapshot(from, to, t);
        return true;
    }

    private WorldSnapshot InterpolateSnapshot(WorldSnapshot from, WorldSnapshot to, float t) {
        int count = 0;

        for (int i = 0; i < from.playerStatesCount; i++) {
            PlayerState fromState = from.playerStates[i];
            bool found = false;
            PlayerState toState = default;

            for (int j = 0; j < to.playerStatesCount; j++) {
                if (to.playerStates[j].id == fromState.id) {
                    toState = to.playerStates[j];
                    found = true;
                    break;
                }
            }

            scratch[count] = new PlayerState {
                id = fromState.id,
                crouching = fromState.crouching,
                position = found
                    ? Vector3.Lerp(fromState.position, toState.position, t)
                    : fromState.position,
            };
            count++;
        }

        for (int i = 0; i < to.playerStatesCount; i++) {
            PlayerState toState = to.playerStates[i];
            bool exists = false;
            for (int j = 0; j < count; j++) {
                if (scratch[j].id == toState.id) {
                    exists = true;
                    break;
                }
            }
            if (!exists) {
                scratch[count] = toState;
                count++;
            }
        }

        return new WorldSnapshot {
            serverTick = from.serverTick,
            playerStates = scratch,
            playerStatesCount = count,
        };
    }

    public void Update() {
    }
}