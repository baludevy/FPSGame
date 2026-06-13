using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class NetworkDebug : MonoBehaviour {
    public static NetworkDebug Instance;

    private Transform holder;
    
    private TMP_Text pingText;
    private TMP_Text byteUpText;
    private TMP_Text byteDownText;
    private TMP_Text packetUpText;
    private TMP_Text packetDownText;
    private TMP_Text timeScaleText;
    private TMP_Text bufferSlackText;

    private float ping;
    private float pSent;
    private float pReceived;
    private float bSent;
    private float bReceived;
    private float timeScale;
    private int bufferSlack;

    private void Awake() {
        Instance = this;
    }

    private void Start() {
        InitUI();
    }

    #region Bandwidth UI

    public void ToggleUI() {
        holder.gameObject.active = !holder.gameObject.active;
    }
    
    private void InitUI() {
        holder = transform.GetChild(0);
        
        pingText = holder.GetChild(0).gameObject.GetComponent<TMP_Text>();
        byteUpText = holder.GetChild(1).gameObject.GetComponent<TMP_Text>();
        byteDownText = holder.GetChild(2).gameObject.GetComponent<TMP_Text>();
        packetUpText = holder.GetChild(3).gameObject.GetComponent<TMP_Text>();
        packetDownText = holder.GetChild(4).gameObject.GetComponent<TMP_Text>();
        timeScaleText = holder.GetChild(5).gameObject.GetComponent<TMP_Text>();
        bufferSlackText = holder.GetChild(6).gameObject.GetComponent<TMP_Text>();

        InvokeRepeating(nameof(Bandwidth), 0f, 1f);
    }
    
    public void SetPing(float a) {
        pingText.text = $"ping: {Mathf.CeilToInt(ping)}ms";
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
        bufferSlackText.text = $"ib slack: {a}";
    }

    private void Bandwidth() {
        ping = PingManager.ping * 1000;
        pSent = ClientSend.packetsSent;
        pReceived = ClientHandle.packetsReceived;
        bSent = ClientSend.bytesSent;
        bReceived = ClientHandle.bytesReceived;
        timeScale = TickTimer.timeScale;
        bufferSlack = TimeScaler.Instance.currentBufferSlack;
        
        SetPing(ping);
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