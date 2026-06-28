using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using PimDeWitte.UnityMainThreadDispatcher;
using UnityEngine;

public class Client {
    private static int dataBufferSize = NetworkSettings.dataBufferSize;
    private const long timeoutTicks = 10 * TimeSpan.TicksPerSecond;

    public int id;
    public Player player;
    public TcpConnection tcp;
    public UdpConnection udp;
    public volatile bool handshakeComplete;
    public byte[] sessionToken { get; private set; }

    private long lastActivityTicks;

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

    public void CompleteHandshake() {
        handshakeComplete = true;
        tcp.CancelHandshakeTimer();
        MarkActivity();
    }

    public void MarkActivity() => Interlocked.Exchange(ref lastActivityTicks, DateTime.UtcNow.Ticks);

    public bool TimedOut(long nowTicks) {
        long last = Interlocked.Read(ref lastActivityTicks);
        return last != 0 && nowTicks - last > timeoutTicks;
    }

    public class TcpConnection {
        public TcpClient socket;
        private readonly int id;
        private NetworkStream stream;
        private Packet receivedData;
        private byte[] receiveBuffer;
        private readonly ConcurrentQueue<byte[]> sendQueue = new();
        private int isSending;
        private volatile bool disconnected;
        private Timer handshakeTimer;

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
            disconnected = false;

            // Slots are reused; clear any leftover state from the previous occupant.
            while (sendQueue.TryDequeue(out _)) {
            }

            Interlocked.Exchange(ref isSending, 0);

            if (Server.clients.TryGetValue(id, out Client owner)) owner.MarkActivity();

            handshakeTimer = new Timer(_ => {
                if (Server.clients.TryGetValue(id, out Client c) && c != null && !c.handshakeComplete) {
                    Debug.Log($"[Server TCP] Client {id} handshake timed out.");
                    TriggerDisconnect();
                }
            }, null, 5000, Timeout.Infinite);

            stream.BeginRead(receiveBuffer, 0, dataBufferSize, ReceiveCallback, null);
            ServerSend.Welcome(id);
        }

        public void CancelHandshakeTimer() {
            try {
                handshakeTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            }
            catch {
            }
        }

        public void SendData(Packet packet) {
            if (disconnected || socket == null) return;
            sendQueue.Enqueue(packet.ToArray());
            TryFlushSendQueue();
        }

        private void TryFlushSendQueue() {
            if (Interlocked.CompareExchange(ref isSending, 1, 0) != 0) return;

            if (!sendQueue.TryDequeue(out byte[] next)) {
                Interlocked.Exchange(ref isSending, 0);
                if (!sendQueue.IsEmpty) TryFlushSendQueue();
                return;
            }

            NetworkStream s = stream;
            if (s == null) {
                Interlocked.Exchange(ref isSending, 0);
                return;
            }

            try {
                s.BeginWrite(next, 0, next.Length, SendCallback, null);
            }
            catch (ObjectDisposedException) {
                Interlocked.Exchange(ref isSending, 0);
            }
            catch (Exception ex) {
                Debug.LogException(ex);
                Interlocked.Exchange(ref isSending, 0);
                TriggerDisconnect();
            }
        }

        private void SendCallback(IAsyncResult result) {
            try {
                stream?.EndWrite(result);
            }
            catch (ObjectDisposedException) {
                Interlocked.Exchange(ref isSending, 0);
                return;
            }
            catch (Exception ex) {
                Debug.LogException(ex);
                Interlocked.Exchange(ref isSending, 0);
                TriggerDisconnect();
                return;
            }

            Interlocked.Exchange(ref isSending, 0);
            TryFlushSendQueue();
        }

        private void ReceiveCallback(IAsyncResult result) {
            if (disconnected) return;
            try {
                int byteLength = stream.EndRead(result);
                if (byteLength <= 0) {
                    TriggerDisconnect();
                    return;
                }

                if (Server.clients.TryGetValue(id, out Client c)) c.MarkActivity();

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
                    Debug.LogWarning($"[Server TCP] Client {id} buffer cap / oversized packet.");
                    TriggerDisconnect();
                    return true;
                },
                dispatch: DispatchPacket);
        }

        private void DispatchPacket(byte[] packetBytes) {
            if (packetBytes == null || packetBytes.Length < 1) return;
            byte packetId = packetBytes[0];

            // Gate everything except welcomeReceived until the handshake is done.
            if (packetId != (byte)ClientPackets.welcomeReceived &&
                Server.clients.TryGetValue(id, out Client c) && c != null && !c.handshakeComplete)
                return;

            PacketDispatch.Route(id, packetBytes, offMainThreadPackets, Server.packetHandlers);
        }

        private void TriggerDisconnect() {
            if (disconnected) return;
            disconnected = true;
            UnityMainThreadDispatcher.Instance().Enqueue(() => Server.clients[id]?.Disconnect());
        }

        public void Disconnect() {
            disconnected = true;
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

            Interlocked.Exchange(ref isSending, 0);
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

        // Pin the endpoint on first valid contact. Returns false if already bound.
        public bool TryBind(IPEndPoint ep) {
            lock (gate) {
                if (endPoint != null) return false;
                endPoint = ep;
                return true;
            }
        }

        public void SendData(Packet packet) {
            IPEndPoint ep;
            lock (gate) {
                ep = endPoint;
            }

            if (ep != null) Server.SendUdpData(ep, packet);
        }

        public void HandleData(Packet packetData) {
            int packetLength = packetData.ReadInt();
            if (packetLength <= 0 || packetLength > NetProtocol.maxPacketSize ||
                packetLength > packetData.UnreadLength()) return;

            byte[] packetBytes = packetData.ReadBytes(packetLength);
            if (packetBytes.Length < 1) return;

            byte packetId = packetBytes[0];
            if (Server.clients.TryGetValue(id, out Client c) && c != null && !c.handshakeComplete)
                return;

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
        Debug.Log($"Spawned player {player.id}.");

        foreach (Client client in Server.clients.Values)
            if (client.player != null && client.id != id)
                ServerSend.SpawnPlayer(id, client.player);
        foreach (Client client in Server.clients.Values)
            if (client.player != null)
                ServerSend.SpawnPlayer(client.id, player);
    }

    public void Disconnect() {
        string label = player != null ? $"{player.username} ({player.id})" : $"Client {id}";
        Debug.Log($"{label} disconnected.");

        UnityMainThreadDispatcher.Instance().Enqueue(() => {
            if (player != null) {
                UnityEngine.Object.Destroy(player.gameObject);
                player = null;
            }
        });

        handshakeComplete = false;
        Interlocked.Exchange(ref lastActivityTicks, 0);
        tcp.Disconnect();
        udp.Disconnect();
    }
}