using System.Diagnostics;
using UnityEngine;


public class NetworkManager : MonoBehaviour {
    public static NetworkManager Instance;

    public static uint tick;

    public GameObject playerPrefab;

    private readonly Stopwatch stopwatch = Stopwatch.StartNew();
    private float accumulator;
    private float currentTime;

    public LagCompensation lagCompensation = new LagCompensation();

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
                    client.player.ProcessInput(input);
                }
            }
        }

        Physics.SyncTransforms();
        Physics.Simulate(NetworkSettings.tickTime);

        lagCompensation.SaveSnapshot(GetWorldSnapshot());
        lagCompensation.Update();

        foreach (Client client in Server.clients.Values) {
            if (client.player != null) {
                SendPlayerUpdate(client.player.id);
            }
        }
    }

    private void SendPlayerUpdate(int toClient) {
        GameUpdate update = new GameUpdate();

        Player toPlayer = Server.clients[toClient].player;

        update.serverTick = tick;
        update.inputBufferOffset = toPlayer.InputBuffer.GetBufferOffset();

        update.clientSendTime = toPlayer.InputBuffer.latestTimestamp;
        update.serverReceiveTime = toPlayer.InputBuffer.latestReceived;

        foreach (Client client in Server.clients.Values) {
            Player player = client.player;

            if (player == null) continue;

            if (player.id == toClient) {
                update.movementState = new MovementState() {
                    id = player.id,
                    position = player.transform.position,
                    orientation = player.movement.orientation.eulerAngles.y,
                    velocity = player.movement.rb.velocity
                };
            }
        }

        update.worldSnapshot = GetWorldSnapshot();

        ServerSend.GameUpdate(toClient, update);
    }

    private WorldSnapshot GetWorldSnapshot() {
        WorldSnapshot snapshot = new WorldSnapshot() {
            tick = tick,
        };

        foreach (Client client in Server.clients.Values) {
            Player player = client.player;

            if (player == null) continue;

            PlayerState playerState = new PlayerState {
                id = player.id,
                position = player.transform.position,
            };

            snapshot.playerStates.Add(playerState);
        }

        return snapshot;
    }

    private WorldSnapshot GetWorldSnapshotForPlayer(int forPlayer) {
        WorldSnapshot snapshot = new WorldSnapshot {
            tick = tick,
        };

        foreach (Client client in Server.clients.Values) {
            Player player = client.player;

            if (player == null || player.id == forPlayer) continue;

            PlayerState playerState = new PlayerState {
                id = player.id,
                position = player.transform.position,
            };

            snapshot.playerStates.Add(playerState);
        }

        return snapshot;
    }

    
    private void OnApplicationQuit() {
        Server.Stop();
    }

    public Player InstantiatePlayer() {
        return Instantiate(playerPrefab, Vector3.zero, Quaternion.identity).GetComponent<Player>();
    }
}