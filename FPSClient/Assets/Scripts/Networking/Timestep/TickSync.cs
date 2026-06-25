using UnityEngine;

public static class TickSync {
    private const int targetSamples = 4;
    private const float pingInterval = 0.01f;

    public static bool syncing;
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

        float now = FixedClock.GetTime();

        if (now - lastPingTime >= pingInterval) {
            lastPingTime = now;
            SendPing(now);
        }
    }

    public static void OnPong(float clientSendTime, uint serverTick) {
        if (!syncing)
            return;
        
        float rtt = FixedClock.GetTime() - clientSendTime;

        uint estimatedTick =
            serverTick +
            (uint)(rtt / 2 / NetworkSettings.tickTime);

        if (rtt < bestRtt) {
            bestRtt = rtt;
            bestTick = estimatedTick;
        }

        samples++;

        if (samples < targetSamples)
            return;

        syncing = false;

        FixedClock.tick = bestTick;
    }

    private static void SendPing(float clientSendTime) {
        ClientSend.SyncTick(clientSendTime);
    }
}