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
    }

    private void Update() {
        timeoutTimer += Time.unscaledDeltaTime;
        if (timeoutTimer >= timeoutCheckInterval) {
            timeoutTimer = 0f;
            Server.CheckTimeouts();
        }
    }

    private void OnApplicationQuit() => Server.Stop();
}