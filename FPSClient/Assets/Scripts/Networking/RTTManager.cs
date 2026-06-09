using System;
using UnityEngine;

public class RTTManager : MonoBehaviour {
    private static long sentTimestamp;
    public static float CurrentRTT { get; private set; }

    public static void SendRTTRequest() {
        sentTimestamp = DateTime.UtcNow.Ticks;
        ClientSend.MeasureRTT(sentTimestamp);
    }

    public static void SendSyncTickRequest() {
        sentTimestamp = DateTime.UtcNow.Ticks;
        ClientSend.SyncTick(sentTimestamp);
    }

    public static void ReceiveRTTResponse(long originalTimestamp) {
        long receivedTimestamp = DateTime.UtcNow.Ticks;

        CurrentRTT = (receivedTimestamp - originalTimestamp) / TimeSpan.TicksPerMillisecond;

        Debug.Log($"RTT: {CurrentRTT} ms");
    }

    public static void SyncTick(long timestamp, int serverTick) {
        long now = DateTime.UtcNow.Ticks;

        double rttMs = (now - timestamp) / (double)TimeSpan.TicksPerMillisecond;

        int latencyTicks = Mathf.CeilToInt(
            (float)(rttMs / 1000.00 / NetworkSettings.tickTime)
        );

        int desiredTick = serverTick + latencyTicks + 2;

        TickTimer.tick = desiredTick;

        Debug.Log(
            $"RTT={rttMs:F1}ms | " +
            $"Server={serverTick} | " +
            $"Client={TickTimer.tick} | "
        );
        
        TickTimer.doTick = true;
    }
}