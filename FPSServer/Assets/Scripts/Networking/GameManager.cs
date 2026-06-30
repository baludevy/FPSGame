using System;
using UnityEngine;

public class GameManager : FixedBehaviour {
    public static GameManager Instance;

    public GameObject playerPrefab;

    [NonSerialized] public LagCompensation lagCompensation;

    private PlayerState[] snapshotBuffer;
    private WorldSnapshot currentSnapshot;

    private void Awake() {
        if (Instance == null) {
            Instance = this;
        }
        else {
            Destroy(gameObject);
        }
    }

    public void Init() {
        snapshotBuffer = new PlayerState[Server.maxPlayers];
        lagCompensation = new LagCompensation();
    }

    public override void UpdateBeforeTick() {
    }

    public override void UpdateFixed() {
        foreach (Client client in Server.clients.Values) {
            if (client.player != null) {
                InputData inputData = client.player.inputBuffer.GetInputFromQueue(FixedClock.tick);
                client.player.MoveInput(inputData);
            }
        }

        Physics.SyncTransforms();
        Physics.Simulate(NetworkSettings.tickTime);

        currentSnapshot = GetWorldSnapshot();

        lagCompensation.SaveSnapshot(currentSnapshot);
        lagCompensation.Update();

        foreach (Client client in Server.clients.Values) {
            if (client.player != null) {
                InputData inputData = client.player.inputBuffer.GetInputFromQueue(FixedClock.tick);
                client.player.OtherInput(inputData);
            }
        }

        foreach (Client client in Server.clients.Values) {
            if (client.player != null) {
                SendPlayerUpdate(client.player.id, currentSnapshot);
            }
        }
    }

    public override void UpdateAfterTick() {
    }

    private void SendPlayerUpdate(int toClient, WorldSnapshot sharedSnapshot) {
        GameUpdate update = new GameUpdate();
        Player toPlayer = Server.clients[toClient].player;

        update.serverTick = FixedClock.tick;
        update.timingInfo = toPlayer.inputBuffer.GetTimingInfo();
        update.upstreamStatistics = toPlayer.monitor.GetUpstreamStatistics();

        update.movementState = new MovementState {
            position = toPlayer.GetState().position,
            velocity = toPlayer.movement.GetRb().linearVelocity,
            orientation = toPlayer.movement.orientation.eulerAngles.y,
        };

        update.worldSnapshot = sharedSnapshot;

        ServerSend.GameUpdate(toClient, update);
    }

    private WorldSnapshot GetWorldSnapshot() {
        int count = 0;

        foreach (Client client in Server.clients.Values) {
            Player player = client.player;
            if (player != null) {
                snapshotBuffer[count] = player.GetState();
                count++;
            }
        }

        return new WorldSnapshot {
            serverTick = FixedClock.tick,
            playerStates = snapshotBuffer,
            playerStatesCount = count,
        };
    }

    public Player InstantiatePlayer() {
        return Instantiate(playerPrefab, Vector3.zero, Quaternion.identity).GetComponent<Player>();
    }
}