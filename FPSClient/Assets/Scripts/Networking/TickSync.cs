using UnityEngine;

public static class TickSync {
    private const int targetSamples = 8;
    private const float pingInterval = 0.05f;

    private static bool syncing;
    private static float lastPingTime;

    private static int samples;
    private static float bestRtt;
    private static uint bestTick;

    public static void StartSync() {
        if (syncing)
            return;

        syncing = true;

        samples = 0;
        bestRtt = float.MaxValue;
        bestTick = 0;

        lastPingTime = -pingInterval;
    }

    public static void Update() {
        if (!syncing)
            return;

        float now = TickTimer.Instance.GetTime();

        if (now - lastPingTime >= pingInterval) {
            lastPingTime = now;
            SendPing(now);
        }
    }

    public static void OnPong(float clientSendTime, uint serverTick) {
        if (!syncing)
            return;

        float rtt = TickTimer.Instance.GetTime() - clientSendTime;

        uint estimatedTick =
            serverTick +
            (uint)(rtt * 2 / NetworkSettings.tickTime);

        if (rtt < bestRtt) {
            bestRtt = rtt;
            bestTick = estimatedTick;
        }

        samples++;

        if (samples < targetSamples)
            return;

        syncing = false;

        TickTimer.tick = bestTick + 2;
        TickTimer.Instance.accumulator = 0f;
    }

    private static void SendPing(float clientSendTime) {
        ClientSend.SyncTick(clientSendTime);
    }
}