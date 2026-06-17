using System.Collections.Generic;
using UnityEngine;

public class SnapshotInterpolator {
    public float renderTick;
    private bool initialized;

    public void Reset() {
        renderTick = 0f;
        initialized = false;
    }

    public bool Advance(SnapshotBuffer buffer, float deltaTime) {
        if (buffer.Count < 2) return false;

        float tickRate = 1f / NetworkSettings.tickTime;
        float interpTicks = NetworkSettings.interpTime * tickRate;

        if (!initialized) {
            renderTick = buffer.Newest.serverTick - interpTicks;
            initialized = true;
            return false;
        }

        AdvanceRenderTick(buffer, deltaTime, tickRate, interpTicks);
        buffer.TrimBefore(renderTick);

        return buffer.Count >= 2;
    }

    private void AdvanceRenderTick(SnapshotBuffer buffer, float deltaTime, float tickRate, float interpTicks) {
        renderTick += deltaTime * tickRate;

        float targetTick = buffer.Newest.serverTick - interpTicks;
        float drift = renderTick - targetTick;

        if (Mathf.Abs(drift) > interpTicks * 2f)
            renderTick = targetTick;
        else
            renderTick -= drift * deltaTime * tickRate * 0.1f;
    }

    public void ApplyToPlayers(SnapshotBuffer buffer, int localPlayerId) {
        buffer.TryGetBounds(renderTick, out WorldSnapshot from, out WorldSnapshot to);

        float tickDelta = to.serverTick - from.serverTick;
        float t = tickDelta > 0f ? (renderTick - from.serverTick) / tickDelta : 0f;
        t = Mathf.Clamp01(t);

        var fromPositions = new Dictionary<int, Vector3>(from.playerStates.Count);
        foreach (PlayerState state in from.playerStates)
            fromPositions[state.id] = state.position;

        foreach (PlayerState toState in to.playerStates) {
            if (toState.id == localPlayerId) continue;
            if (!GameManager.players.TryGetValue(toState.id, out PlayerManager player) || player == null) continue;

            Vector3 fromPos = fromPositions.TryGetValue(toState.id, out Vector3 pos) ? pos : toState.position;
            
            player.transform.position = Vector3.Lerp(fromPos, toState.position, t);
            player.transform.localScale = toState.crouching
                ? new Vector3(1.25f, 1.25f, 1.25f)
                : new Vector3(1.25f, 1.75f, 1.25f);
        }
    }
}