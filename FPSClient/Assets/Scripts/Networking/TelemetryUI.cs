using TMPro;
using UnityEngine;

public class TelemetryUI : MonoBehaviour {
    public TMP_Text telemetryText;

    private float bytesSentPerSecond;
    private float bytesReceivedPerSecond;
    private int packetsSentPerSecond;
    private int packetsReceivedPerSecond;

    private float lastBytesSent;
    private float lastBytesReceived;
    private float lastPacketsSent;
    private float lastPacketsReceived;

    private float timer;

    private void Update() {
        timer += Time.deltaTime;

        if (timer >= 1f) {
            UpdateBandwidth(timer);

            timer = 0f;
        }

        UpdateTelemetry();
    }

    private void UpdateBandwidth(float elapsedTime) {
        bytesSentPerSecond = (NetStatistics.bytesSent - lastBytesSent) / elapsedTime;
        bytesReceivedPerSecond = (NetStatistics.bytesReceived - lastBytesReceived) / elapsedTime;

        packetsSentPerSecond = Mathf.RoundToInt((NetStatistics.packetsSent - lastPacketsSent) / elapsedTime);
        packetsReceivedPerSecond =
            Mathf.RoundToInt((NetStatistics.packetsReceived - lastPacketsReceived) / elapsedTime);

        lastBytesSent = NetStatistics.bytesSent;
        lastBytesReceived = NetStatistics.bytesReceived;
        lastPacketsSent = NetStatistics.packetsSent;
        lastPacketsReceived = NetStatistics.packetsReceived;
    }

    private void UpdateTelemetry() {
        int fps = (int)(1.0f / Time.deltaTime);
        float msec = Time.deltaTime * 1000.0f;

        string str = $"{fps} fps ({msec:F1} ms) ping: {NetStatistics.ping * 1000f:F0} ms\n" +
                     $"jitter: {Mathf.FloorToInt(NetStatistics.upstreamJitter * 1000f)}/{Mathf.FloorToInt(NetStatistics.downstreamJitter * 1000f)} ms  loss: {NetStatistics.upstreamPacketLoss * 100:F0}/{NetStatistics.downstreamPacketLoss * 100:F0}\n" +
                     $"up: {FormatBytes(bytesSentPerSecond)}/s  down: {FormatBytes(bytesReceivedPerSecond)}/s\n" +
                     $"up: {packetsSentPerSecond}/s  down: {packetsReceivedPerSecond}/s\n" +
                     $"tick: {FixedClock.tick}  rate: {NetworkSettings.tickRate * FixedClock.timeScale:F2}\n" +
                     $"srv margin: {TimeScaler.GetCurrentMargin() * 1000F:F1} ms  target: {NetworkSettings.targetInputMargin * 1000:F1} ms";

        if (LocalPlayer.Instance != null)
            str +=
                $"\npos: {LocalPlayer.Instance.movement.transform.position:F1} vel: {LocalPlayer.Instance.movement.GetRb().velocity.magnitude:F1} u/s";

        telemetryText.text = str;
    }

    private string FormatBytes(float bytes) {
        if (bytes < 1024)
            return $"{bytes:F0} B";

        if (bytes < 1024 * 1024)
            return $"{bytes / 1024f:F1} KB";

        return $"{bytes / (1024f * 1024f):F1} MB";
    }
}