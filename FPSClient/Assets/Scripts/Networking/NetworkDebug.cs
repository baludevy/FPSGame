using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class NetworkDebug : MonoBehaviour {
    public static NetworkDebug Instance;

    private Transform holder;

    private TMP_Text pingText;
    private TMP_Text jitterText;
    private TMP_Text packetLossText;
    private TMP_Text byteUpText;
    private TMP_Text byteDownText;
    private TMP_Text packetUpText;
    private TMP_Text packetDownText;
    private TMP_Text timeScaleText;
    private TMP_Text bufferSlackText;

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
        holder = transform.GetChild(0);

        pingText = holder.GetChild(0).gameObject.GetComponent<TMP_Text>();
        jitterText = holder.GetChild(1).gameObject.GetComponent<TMP_Text>();
        packetLossText = holder.GetChild(2).gameObject.GetComponent<TMP_Text>();
        byteUpText = holder.GetChild(3).gameObject.GetComponent<TMP_Text>();
        byteDownText = holder.GetChild(4).gameObject.GetComponent<TMP_Text>();
        packetUpText = holder.GetChild(5).gameObject.GetComponent<TMP_Text>();
        packetDownText = holder.GetChild(6).gameObject.GetComponent<TMP_Text>();
        timeScaleText = holder.GetChild(7).gameObject.GetComponent<TMP_Text>();
        bufferSlackText = holder.GetChild(8).gameObject.GetComponent<TMP_Text>();

        InvokeRepeating(nameof(Bandwidth), 0f, 1f);
    }

    public void SetPing(float a) {
        pingText.text = $"ping: {Mathf.CeilToInt(a)}ms";
    }

    public void SetJitter(float a) {
        jitterText.text = $"jitter: {Mathf.CeilToInt(a)}ms";
    }

    public void SetPacketLoss(float a) {
        packetLossText.text = $"packet loss: {a}%";
    }

    private void SetByteUp(float a) {
        byteUpText.text = $"bytes up/s: {a}";
    }

    private void SetByteDown(float a) {
        byteDownText.text = $"bytes down/s: {a}";
    }

    private void SetPacketUp(float a) {
        packetUpText.text = $"packets up/s: {a}";
    }

    private void SetPacketDown(float a) {
        packetDownText.text = $"packets down/s: {a}";
    }

    private void SetTimeScale(float a) {
        timeScaleText.text = $"game speed: {a}";
    }

    private void SetBufferSlack(int a) {
        bufferSlackText.text = $"buffer off: {a}";
    }

    private void Bandwidth() {
        float ping = ConnectionStats.ping;
        float jitter = ConnectionStats.jitter;
        float packetLoss = ConnectionStats.packetLoss;
        float pSent = ClientSend.packetsSent;
        float pReceived = ClientHandle.packetsReceived;
        float bSent = ClientSend.bytesSent;
        float bReceived = ClientHandle.bytesReceived;
        float timeScale = TickTimer.timeScale;
        int bufferSlack = TimeScaler.Instance.currentBufferSlack;

        SetPing(ping);
        SetJitter(jitter);
        SetPacketLoss(packetLoss);
        SetByteUp(bSent);
        SetByteDown(bReceived);
        SetPacketUp(pSent);
        SetPacketDown(pReceived);
        SetTimeScale(timeScale);
        SetBufferSlack(bufferSlack);

        ClientSend.packetsSent = 0;
        ClientHandle.packetsReceived = 0;
        ClientSend.bytesSent = 0;
        ClientHandle.bytesReceived = 0;
    }

    #endregion
}