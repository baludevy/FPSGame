using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class NetworkDebug : MonoBehaviour {
    public static NetworkDebug Instance;

    private Transform holder;

    //ping
    public TMP_Text ping;
    public TMP_Text totalPing;

    //bandwidth
    public TMP_Text bandUp;
    public TMP_Text bandDown;

    //packets
    public TMP_Text packetUp;
    public TMP_Text packetDown;

    //jitter and loss
    public TMP_Text packetLoss;
    public TMP_Text jitter;
    public TMP_Text inputJitter;

    //input buffer
    public TMP_Text inputBufferOffset;
    public TMP_Text inputBufferTarget;

    //snapshot buffer
    public TMP_Text snapshotBufferOffset;
    public TMP_Text snapshotBufferTarget;

    //client tick
    public TMP_Text clientTick;
    public TMP_Text clientTimescale;

    //server tick
    public TMP_Text serverTick;
    public TMP_Text renderTick;

    private void Awake() {
        Instance = this;
    }

    private void Start() {
        InitUI();
    }

    #region Bandwidth UI

    public void ToggleUI() {
        // holder.gameObject.active = !holder.gameObject.active;
    }

    private void InitUI() {
        InvokeRepeating(nameof(Bandwidth), 0f, 1f);
    }

    public void Update() {
        OtherUI();
    }


    private void Bandwidth() {
        bandUp.text = $"up: {ClientSend.bytesSent:F0} b/s";
        bandDown.text = $"down: {ClientHandle.bytesReceived:F0} b/s";

        packetUp.text = $"up: {ClientSend.packetsSent:F0}/s";
        packetDown.text = $"down: {ClientHandle.packetsReceived:F0}/s";


        ClientSend.packetsSent = 0;
        ClientHandle.packetsReceived = 0;
        ClientSend.bytesSent = 0;
        ClientHandle.bytesReceived = 0;
    }

    private void OtherUI() {
        ping.text = $"ping: {ConnectionStatistics.ping * 1000f:F0}ms";
        totalPing.text = $"total: {ConnectionStatistics.totalRtt * 1000f:F0}ms";

        packetLoss.text = $"loss: {ConnectionStatistics.packetLoss * 100:F0}%";
        jitter.text = $"client jitter: {ConnectionStatistics.jitter * 1000:F0}ms";
        inputJitter.text = $"server jitter: {ConnectionStatistics.inputJitter * 1000:F0}ms";

        inputBufferOffset.text = $"srv receive: {TimeScaler.GetCurrentMargin() * 1000:F0} ms";
        inputBufferTarget.text = $"target: {NetworkSettings.targetServerMargin * 1000:F0} ms";

        // snapshotBufferOffset.text = $"cl receive: {SnapshotManager.snapshotBufferOffset}";
        snapshotBufferTarget.text = $"target: {NetworkSettings.interpTime / NetworkSettings.tickTime}";

        clientTick.text = $"cl tick: {FixedClock.tick}";
        clientTimescale.text = $"rate: {NetworkSettings.tickRate * FixedClock.timeScale:F2}";

        serverTick.text = $"srv tick: {SnapshotManager.serverTick}";
        // renderTick.text = $"rnd tick: {Mathf.RoundToInt(SnapshotManager.clientRenderTick)}";
    }

    #endregion
}