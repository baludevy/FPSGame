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
}