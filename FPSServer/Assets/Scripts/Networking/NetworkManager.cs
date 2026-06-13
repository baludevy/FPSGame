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
        while (timer >= NetworkSettings.tickTime) {
            timer -= NetworkSettings.tickTime;

            ProcessTick();
            tick++;
        }
    }

    private void ProcessTick() {
        ThreadManager.UpdateMain();

        foreach (Client client in Server.clients.Values) {
            if (client.player != null) {
                PlayerInput input = client.player.InputBuffer.GetInputFromQueue(tick);

                if (input != null)
                    client.player.movement.SetInputs(input.x, input.y, input.orientation, input.jumping,
                        input.crouching);

                client.player.movement.AdvanceLogic();

                // Debug.Log($"applying movement with x:{input.x},y:{input.y},j:{input.jumping},{input.crouching}, tick:{input.tick}");
            }
        }

        Physics.SyncTransforms();

        //Forward physics simulation by one step

        Physics.Simulate(NetworkSettings.tickTime);

        foreach (Client client in Server.clients.Values) {
            if (client.player != null) {
                SendWorldSnapshot(client.player.id);
            }
        }
    }

    private void SendWorldSnapshot(int toClient) {
        WorldSnapshot snapshot = new WorldSnapshot();

        Player toPlayer = Server.clients[toClient].player;

        snapshot.serverTick = tick;
        snapshot.bufferSlack = toPlayer.InputBuffer.GetBufferSlack();
        snapshot.echoTimestamp = toPlayer.InputBuffer.latestTimestamp;

        foreach (Client client in Server.clients.Values) {
            Player player = client.player;

            if (player != null) {
                PlayerState playerState = new PlayerState {
                    id = player.id,
                    position = player.transform.position,
                    velocity = player.movement.rb.velocity,
                    orientation = player.movement.orientation.transform.eulerAngles.y,
                };

                snapshot.playerStates.Add(playerState);
            }
        }

        ServerSend.WorldSnapshot(toClient, snapshot);
    }

    private void OnApplicationQuit() {
        Server.Stop();
    }

    public Player InstantiatePlayer() {
        return Instantiate(playerPrefab, Vector3.zero, Quaternion.identity).GetComponent<Player>();
    }
}