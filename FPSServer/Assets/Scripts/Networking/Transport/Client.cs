using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using PimDeWitte.UnityMainThreadDispatcher;
using UnityEngine;

public class Client {
    private static int dataBufferSize = NetworkSettings.dataBufferSize;
    private const long timeoutTicks = 10 * TimeSpan.TicksPerSecond;

    public int id;
    public string username;
    public Player player;
    public TcpConnection tcp;
    public UdpConnection udp;

    public SafeFlag welcomeAcked = new(); // got username with welcome packet sent over tcp
    public SafeFlag udpBound = new(); // the first valid udp packet was received, endpoint was bound
    public SafeFlag inGame = new(); // player was spawned

    private SafeLatch spawnLatch = new();
    private SafeLong lastActivityTicks = new();

    public byte[] sessionToken { get; private set; }

    private static readonly HashSet<byte> offMainThreadPackets = new() {
        (byte)ClientPackets.playerInput
    };

    public Client(int clientId) {
        id = clientId;
        tcp = new TcpConnection(id);
        udp = new UdpConnection(id);
    }

    public byte[] IssueNewToken() {
        sessionToken = NetProtocol.NewToken();
        return sessionToken;
    }

    // client acknowledged welcome over tcp. we have a username now, udp may not be proven yet
    public void MarkWelcomeAcked() {
        welcomeAcked.Set();
        MarkActivity();
        TrySpawn();
    }

    public void MarkUdpBound() {
        if (udpBound.IsSet()) {
            return;
        }

        udpBound.Set();
        MarkActivity();
        ServerSend.UdpConfirmed(id);
        TrySpawn();
    }

    private void TrySpawn() {
        if (!welcomeAcked.IsSet() || !udpBound.IsSet()) {
            return;
        }

        if (!spawnLatch.TrySet()) {
            return;
        }

        inGame.Set();
        tcp.CancelHandshakeTimer();

        int clientId = id;
        UnityMainThreadDispatcher.Instance().Enqueue(() => { NetworkManager.Instance.OnClientConnected(clientId); });
    }

    public void MarkActivity() {
        lastActivityTicks.Set(DateTime.UtcNow.Ticks);
    }

    public bool TimedOut(long nowTicks) {
        if (!inGame.IsSet()) {
            return false;
        }

        long last = lastActivityTicks.Get();
        if (last == 0) {
            return false;
        }

        return nowTicks - last > timeoutTicks;
    }

    public class TcpConnection {
        public TcpClient socket;
        private readonly int id;
        private NetworkStream stream;
        private Packet receivedData;
        private byte[] receiveBuffer;
        private readonly ConcurrentQueue<byte[]> sendQueue = new();
        private SendGate sendGate = new();
        private SafeFlag disconnected = new();
        private System.Threading.Timer handshakeTimer;

        public TcpConnection(int id) {
            this.id = id;
        }

        public void Connect(TcpClient tcpSocket) {
            socket = tcpSocket;
            socket.ReceiveBufferSize = dataBufferSize;
            socket.SendBufferSize = dataBufferSize;
            stream = socket.GetStream();
            receivedData = new Packet();
            receiveBuffer = new byte[dataBufferSize];
            disconnected.Clear();

            while (sendQueue.TryDequeue(out _)) {
            }

            sendGate.Exit();

            if (Server.clients.TryGetValue(id, out Client owner)) {
                owner.MarkActivity();
            }

            handshakeTimer = new System.Threading.Timer(_ => {
                if (Server.clients.TryGetValue(id, out Client c) && c != null && !c.inGame.IsSet()) {
                    Debug.Log($"Client {id} handshake timed out before entering game");
                    TriggerDisconnect();
                }
            }, null, 5000, System.Threading.Timeout.Infinite);

            stream.BeginRead(receiveBuffer, 0, dataBufferSize, ReceiveCallback, null);
            ServerSend.Welcome(id);
        }

        public void CancelHandshakeTimer() {
            try {
                handshakeTimer?.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
            }
            catch {
            }
        }

        public void SendData(Packet packet) {
            if (disconnected.IsSet() || socket == null) {
                return;
            }

            sendQueue.Enqueue(packet.ToArray());
            TryFlushSendQueue();
        }

        private void TryFlushSendQueue() {
            if (!sendGate.TryEnter()) {
                return;
            }

            if (!sendQueue.TryDequeue(out byte[] next)) {
                sendGate.Exit();
                if (!sendQueue.IsEmpty) {
                    TryFlushSendQueue();
                }

                return;
            }

            NetworkStream s = stream;
            if (s == null) {
                sendGate.Exit();
                return;
            }

            try {
                s.BeginWrite(next, 0, next.Length, SendCallback, null);
            }
            catch (ObjectDisposedException) {
                sendGate.Exit();
            }
            catch (Exception ex) {
                Debug.LogException(ex);
                
                sendGate.Exit();
                TriggerDisconnect();
            }
        }

        private void SendCallback(IAsyncResult result) {
            try {
                stream?.EndWrite(result);
            }
            catch (ObjectDisposedException) {
                sendGate.Exit();
                return;
            }
            catch (Exception ex) {
                Debug.LogException(ex);
                sendGate.Exit();
                TriggerDisconnect();
                return;
            }

            sendGate.Exit();
            TryFlushSendQueue();
        }

        private void ReceiveCallback(IAsyncResult result) {
            if (disconnected.IsSet()) {
                return;
            }

            try {
                int byteLength = stream.EndRead(result);
                if (byteLength <= 0) {
                    TriggerDisconnect();
                    return;
                }

                if (Server.clients.TryGetValue(id, out Client c)) {
                    c.MarkActivity();
                }

                byte[] data = new byte[byteLength];
                Buffer.BlockCopy(receiveBuffer, 0, data, 0, byteLength);
                receivedData.Reset(HandleData(data));
                stream.BeginRead(receiveBuffer, 0, dataBufferSize, ReceiveCallback, null);
            }
            catch (ObjectDisposedException) {
                TriggerDisconnect();
            }
            catch (Exception ex) {
                Debug.LogException(ex);
                TriggerDisconnect();
            }
        }

        private bool HandleData(byte[] data) {
            return TcpFraming.Process(
                receivedData,
                data,
                onOverflow: () => {
                    Debug.LogWarning($"Client {id} sent oversized packet");
                    TriggerDisconnect();
                    return true;
                },
                dispatch: DispatchPacket);
        }

        private void DispatchPacket(byte[] packetBytes) {
            if (packetBytes == null || packetBytes.Length < 1) {
                return;
            }

            byte packetId = packetBytes[0];

            if (packetId != (byte)ClientPackets.welcomeReceived &&
                Server.clients.TryGetValue(id, out Client client) && client != null && !client.welcomeAcked.IsSet()) {
                return;
            }

            PacketDispatch.Route(id, packetBytes, offMainThreadPackets, Server.packetHandlers);
        }

        private void TriggerDisconnect() {
            if (disconnected.IsSet()) {
                return;
            }

            disconnected.Set();
            UnityMainThreadDispatcher.Instance().Enqueue(() => { Server.clients[id]?.Disconnect(); });
        }

        public void Disconnect() {
            disconnected.Set();

            try {
                handshakeTimer?.Dispose();
            }
            catch {
            }

            handshakeTimer = null;

            try {
                socket?.Close();
            }
            catch {
            }

            receiveBuffer = null;

            try {
                receivedData?.Dispose();
            }
            catch {
            }

            while (sendQueue.TryDequeue(out _)) {
            }

            sendGate.Exit();
            stream = null;
            receivedData = null;
            socket = null;
        }
    }

    public class UdpConnection {
        private readonly int id;
        private readonly object gate = new();
        private IPEndPoint endPoint;

        public UdpConnection(int id) {
            this.id = id;
        }

        public IPEndPoint GetEndPoint() {
            lock (gate) {
                return endPoint;
            }
        }

        public bool TryBind(IPEndPoint ep) {
            lock (gate) {
                if (endPoint != null) {
                    return false;
                }

                endPoint = ep;
                return true;
            }
        }

        public void SendData(Packet packet) {
            IPEndPoint ep;
            lock (gate) {
                ep = endPoint;
            }

            if (ep != null) {
                Server.SendUdpData(ep, packet);
            }
        }

        public void HandleData(Packet packetData) {
            int packetLength = packetData.ReadInt();
            if (packetLength <= 0 || packetLength > NetProtocol.maxPacketSize ||
                packetLength > packetData.UnreadLength()) {
                return;
            }

            byte[] packetBytes = packetData.ReadBytes(packetLength);
            if (packetBytes.Length < 1) {
                return;
            }

            // gameplay udp is only processed once the client is fully in-game
            if (Server.clients.TryGetValue(id, out Client client) && client != null && !client.inGame.IsSet()) {
                return;
            }

            PacketDispatch.Route(id, packetBytes, offMainThreadPackets, Server.packetHandlers);
        }

        public void Disconnect() {
            lock (gate) {
                endPoint = null;
            }
        }
    }

    public void SendIntoGame(string playerName) {
        player = GameManager.Instance.InstantiatePlayer();
        player.Initialize(id, playerName);
        Debug.Log($"Spawned Player {player.id} ({playerName})");

        foreach (Client client in Server.clients.Values) {
            if (client.player != null && client.id != id) {
                ServerSend.SpawnPlayer(id, client.player);
            }
        }

        foreach (Client client in Server.clients.Values) {
            if (client.player != null && client.inGame.IsSet()) {
                ServerSend.SpawnPlayer(client.id, player);
            }
        }
    }

    public void Disconnect() {
        UnityMainThreadDispatcher.Instance().Enqueue(() => { NetworkManager.Instance.OnClientDisconnected(id); });

        welcomeAcked.Clear();
        udpBound.Clear();
        inGame.Clear();
        spawnLatch.Reset();

        username = null;
        lastActivityTicks.Set(0);

        tcp.Disconnect();
        udp.Disconnect();
    }
}