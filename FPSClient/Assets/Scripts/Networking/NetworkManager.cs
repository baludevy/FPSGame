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

    public void Connect(string ip = "127.0.0.1", int tcpPort = 42069, int udpPort = 42069) {
        if (currentState != State.disconnected) {
            return;
        }

        currentState = State.connecting;
        Debug.Log($"Connecting to {ip} (tcp: {tcpPort}, udp: {udpPort})");
        Client.Instance.ConnectToServer(ip, tcpPort, udpPort);
    }

    public void NotifyConnected() {
        if (currentState == State.connected) {
            return;
        }

        currentState = State.connected;
        OnConnected();
    }

    public void NotifyDisconnected() {
        if (currentState == State.disconnected) {
            return;
        }

        currentState = State.disconnected;
        OnDisconnected();
    }

    public void Disconnect() {
        if (currentState == State.disconnected) {
            return;
        }

        Client.Instance.Disconnect();
    }

    private void OnConnected() {
        NetworkUIManager.Instance.DisableConnectUI();
        FixedClock.Reset();

        Debug.Log("Connected");
    }

    private void OnDisconnected() {
        NetworkUIManager.Instance.EnableConnectUI();
        CursorManager.EnableCursor();

        NetStatisticsManager.Reset();
        AdaptiveNetcode.Reset();
        UpdateManager.Instance.Reset();
        UpdateDeserializer.Reset();
        
        FixedClock.Reset();

        foreach (PlayerManager player in GameManager.players.Values) {
            Destroy(player.gameObject);
        }

        GameManager.players.Clear();

        Debug.Log("Disconnected OwO");
    }

    private void OnApplicationQuit() {
        if (currentState != State.disconnected) {
            Client.Instance.Disconnect();
        }
    }
}