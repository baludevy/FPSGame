using UnityEngine;

public class GameManager : FixedBehaviour {
    public static GameManager Instance;

    public GameObject playerPrefab;

    public LagCompensation lagCompensation = new LagCompensation();

    private void Awake() {
        if (Instance == null) {
            Instance = this;
        }
        else {
            Destroy(gameObject);
        }
    }

    public override void UpdateFixed() {
        foreach (Client client in Server.clients.Values) {
            if (client.player != null) {
                PlayerInput input = client.player.inputBuffer.GetInputFromQueue(FixedClock.tick);
                client.player.HandleInput(input);
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

        update.serverTick = FixedClock.tick;
        update.serverSendTime = FixedClock.GetTime();
        update.inputBufferSize = toPlayer.inputBuffer.GetBufferSize();

        update.clientSendTime = toPlayer.inputBuffer.latestTimestamp;
        update.serverReceiveTime = toPlayer.inputBuffer.latestReceived;

        foreach (Client client in Server.clients.Values) {
            Player player = client.player;

            if (player == null) continue;

            if (player.id == toClient) {
                update.movementState = new MovementState() {
                    id = player.id,
                    position = player.transform.position,
                    orientation = player.movement.orientation.eulerAngles.y,
                    velocity = player.movement.GetRb().velocity
                };
            }
        }

        update.worldSnapshot = GetWorldSnapshot();

        ServerSend.GameUpdate(toClient, update);
    }


    private WorldSnapshot GetWorldSnapshot() {
        WorldSnapshot snapshot = new WorldSnapshot() {
            tick = FixedClock.tick,
        };

        foreach (Client client in Server.clients.Values) {
            Player player = client.player;

            if (player != null) {
                snapshot.playerStates.Add(player.GetState());
            }
        }

        return snapshot;
    }

    private WorldSnapshot GetWorldSnapshotForPlayer(int forPlayer) {
        WorldSnapshot snapshot = new WorldSnapshot {
            tick = FixedClock.tick,
        };

        foreach (Client client in Server.clients.Values) {
            Player player = client.player;

            if (player != null || player.id != forPlayer) {
                snapshot.playerStates.Add(player.GetState());
            }
        }

        return snapshot;
    }

    public Player InstantiatePlayer() {
        return Instantiate(playerPrefab, Vector3.zero, Quaternion.identity).GetComponent<Player>();
    }
}