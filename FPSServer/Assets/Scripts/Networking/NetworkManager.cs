using UnityEngine;


public class NetworkManager : MonoBehaviour {
    public static NetworkManager Instance;

    private float timer;
    public static int tick;

    public GameObject playerPrefab;

    private void Awake() {
        Instance = this;
    }

    private void Start() {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = NetworkSettings.tickRate;
        Server.Start(10, 42069);
    }

    private void Update() {
        timer += Time.deltaTime;
        if (timer >= NetworkSettings.tickTime) {
            timer -= NetworkSettings.tickTime;

            ProcessTick();
            tick++;
        }
    }

    private void ProcessTick() {
        ThreadManager.UpdateMain();

        foreach (Client client in Server.clients.Values) {
            if (client.player != null) {
                ServerSend.PlayerPosition(client.player.id, client.player.transform.position);
            }
        }
    }

    private void OnApplicationQuit() {
        Server.Stop();
    }

    public Player InstantiatePlayer() {
        return Instantiate(playerPrefab, Vector3.zero, Quaternion.identity).GetComponent<Player>();
    }
}