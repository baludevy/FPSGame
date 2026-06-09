using System;
using UnityEngine;

public class RTTManager : MonoBehaviour {
    private static double timestamp;

    public static float currentRtt;

    public static void SendRTTRequest() {
        timestamp = TickTimer.Instance.GetTime();
        ClientSend.MeasureRTT(timestamp);
    }

    public static void SendSyncTickRequest() {
        timestamp = TickTimer.Instance.GetTime();
        ClientSend.SyncTick(timestamp);
    }

    public static void ReceiveRTTResponse(double originalTimestamp) {
        double receivedTimestamp = TickTimer.Instance.GetTime();

        currentRtt = (float)((receivedTimestamp - originalTimestamp) * 1000.0);

        Debug.Log($"RTT: {currentRtt} ms");
    }

    public static void SyncTick(double timestamp, int serverTick) {
        double now = TickTimer.Instance.GetTime();

        double rttMs = (now - timestamp) * 1000.0;
        currentRtt = (float)rttMs;

        int latencyTicks = Mathf.CeilToInt(
            (float)(rttMs / 2.0 / 1000.00 / NetworkSettings.tickTime)
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