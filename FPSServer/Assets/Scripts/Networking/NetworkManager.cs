using UnityEngine;


public class NetworkManager : MonoBehaviour {
    public static NetworkManager Instance;

    private float timer;
    public static int tick;

    public GameObject playerPrefab;

    public int movementPacketsReceivedInTick;

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
        while (timer >= NetworkSettings.tickTime)
        {
            timer -= NetworkSettings.tickTime;

            // ProcessTick();
            tick++;
        }
    }

    private void FixedUpdate() {
        ThreadManager.UpdateMain();
        
        //Forward physics simulation by one step
        Physics.Simulate(NetworkSettings.tickTime);

        foreach (Client client in Server.clients.Values) {
            if (client.player != null) {
                ServerSend.PlayerPosition(client.player.id, client.player.transform.position);
            }
        }
        
        // Debug.Log(movementPacketsReceivedInTick);
        
        movementPacketsReceivedInTick = 0;
    }

    private void OnApplicationQuit() {
        Server.Stop();
    }

    public Player InstantiatePlayer() {
        return Instantiate(playerPrefab, Vector3.zero, Quaternion.identity).GetComponent<Player>();
    }
}