using System.Diagnostics;
using UnityEngine;


public class NetworkManager : MonoBehaviour {
    public static NetworkManager Instance;

    public static uint tick;

    public GameObject playerPrefab;

    private readonly Stopwatch stopwatch = Stopwatch.StartNew();
    private float accumulator;
    private float currentTime;

    private void Awake() {
        Instance = this;
    }

    private void Start() {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = NetworkSettings.tickRate;

        Server.Start(10, 42069);

        currentTime = GetTime();
    }

    private void Update() {
        float newTime = GetTime();
        float frameTime = newTime - currentTime;
        currentTime = newTime;

        accumulator += frameTime;

        while (accumulator >= NetworkSettings.tickTime) {
            accumulator -= NetworkSettings.tickTime;

            ProcessTick();
            tick++;
        }
    }

    public float GetTime() {
        return (float)stopwatch.Elapsed.TotalSeconds;
    }

    private void ProcessTick() {
        ThreadManager.UpdateMain();

        foreach (Client client in Server.clients.Values) {
            if (client.player != null) {
                PlayerInput input = client.player.InputBuffer.GetInputFromQueue(tick);

                if (input != null) {
                    client.player.movement.SetInput(
                        input.x,
                        input.y,
                        input.orientation,
                        input.jumping,
                        input.crouching
                    );
                }

                client.player.movement.AdvanceLogic();
            }
        }

        Physics.SyncTransforms();
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
        snapshot.inputBufferOffset = toPlayer.InputBuffer.GetBufferOffset();

        snapshot.clientSendTime = toPlayer.InputBuffer.latestTimestamp;
        snapshot.serverReceiveTime = toPlayer.InputBuffer.latestReceived;

        foreach (Client client in Server.clients.Values) {
            Player player = client.player;

            if (player == null) continue;

            if (player.id == toClient) {
                snapshot.movementState = new MovementState() {
                    id = player.id,
                    position = player.transform.position,
                    orientation = player.movement.orientation.eulerAngles.y,
                    velocity = player.movement.rb.velocity
                };
                continue;
            }

            PlayerState playerState = new PlayerState {
                id = player.id,
                position = player.transform.position,
            };

            snapshot.playerStates.Add(playerState);
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