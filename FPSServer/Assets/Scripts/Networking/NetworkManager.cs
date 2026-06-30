using UnityEngine;

public class NetworkManager : MonoBehaviour {
    public static NetworkManager Instance;

    public int maxPlayers = 10;
    public int port = 42069;
    public float timeoutCheckInterval = 1f;

    private float timeoutTimer;

    private void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start() {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = NetworkSettings.tickRate * 3;

        Server.Start(maxPlayers, port);
        GameManager.Instance.Init();
    }

    private void Update() {
        timeoutTimer += Time.unscaledDeltaTime;
        if (timeoutTimer >= timeoutCheckInterval) {
            timeoutTimer = 0f;
            Server.CheckTimeouts();
        }
    }

    public void OnClientConnected(int clientId) {
        if (Server.clients.TryGetValue(clientId, out Client client)) {
            Debug.Log($"Client {clientId} connected");
            
            client.SendIntoGame(client.username);
        }
    }

    public void OnClientDisconnected(int clientId) {
        if (Server.clients.TryGetValue(clientId, out Client client)) {
            Debug.Log($"Client {clientId} disconnected");

            if (client.player != null) {
                Destroy(client.player.gameObject);
                client.player = null;
            }
        }
    }

    private void OnApplicationQuit() {
        Server.Stop();
    }
}