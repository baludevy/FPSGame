using System;
using UnityEngine;

public class NetworkManager : MonoBehaviour {
    public static NetworkManager Instance;

    public enum State {
        disconnected,
        connecting,
        connected
    }

    public State currentState = State.disconnected;

    private void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = NetworkSettings.tickRate * 3;
    }

    public void Connect(string ip = "127.0.0.1") {
        if (currentState != State.disconnected) return;

        currentState = State.connecting;
        Client.Instance.ConnectToServer(ip);
    }

    public void NotifyConnected() {
        currentState = State.connected;
        OnConnected();
    }

    public void NotifyDisconnected() {
        if (currentState == State.disconnected) return;

        currentState = State.disconnected;
        OnDisconnected();
    }

    public void Disconnect() {
        if (currentState == State.disconnected) return;
        
        Client.Instance.Disconnect();
    }

    private void OnConnected() {
        ClientSend.WelcomeReceived();
        NetworkUIManager.Instance.DisableConnectUI();

        FixedClock.Reset();
        TickSync.StartSync();

        Debug.Log("Connected.");
    }

    private void OnDisconnected() {
        foreach (PlayerManager player in GameManager.players.Values) {
            Destroy(player.gameObject);
        }

        GameManager.players.Clear();

        NetworkUIManager.Instance.EnableConnectUI();
        CursorManager.EnableCursor();

        NetStatisticsManager.Reset();
        AdaptiveNetcode.Reset();
        UpdateManager.Instance.Reset();

        FixedClock.Reset();

        Debug.Log("Cleared state.");
        Debug.Log("Disconnected.");
    }

    private void OnApplicationQuit() {
        if (currentState != State.disconnected) Client.Instance.Disconnect();
    }
}