using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

public class Client {
    private static int dataBufferSize = NetworkSettings.dataBufferSize;

    public int id;
    public Player player;
    public TCP tcp;
    public UDP udp;

    private static readonly HashSet<int> _offMainThreadPackets = new() {
        (int)ClientPackets.playerInput
    };

    public Client(int clientId) {
        id = clientId;
        tcp = new TCP(id);
        udp = new UDP(id);
    }

    public class TCP {
        public TcpClient Socket;

        private readonly int _id;
        private NetworkStream _stream;
        private Packet _receivedData;
        private byte[] _receiveBuffer;

        private readonly ConcurrentQueue<byte[]> _sendQueue = new();
        private int _isSending;
        private volatile bool _disconnected;

        public TCP(int id) {
            _id = id;
        }

        public void Connect(TcpClient socket) {
            Socket = socket;
            Socket.ReceiveBufferSize = dataBufferSize;
            Socket.SendBufferSize = dataBufferSize;

            _stream = Socket.GetStream();
            _receivedData = new Packet();
            _receiveBuffer = new byte[dataBufferSize];
            _disconnected = false;

            _stream.BeginRead(_receiveBuffer, 0, dataBufferSize, ReceiveCallback, null);
            ServerSend.Welcome(_id);
        }

        public void SendData(Packet packet) {
            if (_disconnected || Socket == null) return;

            byte[] data = packet.ToArray();

            _sendQueue.Enqueue(data);
            TryFlushSendQueue();
        }

        private void TryFlushSendQueue() {
            if (Interlocked.CompareExchange(ref _isSending, 1, 0) != 0) return;

            if (!_sendQueue.TryDequeue(out byte[] next)) {
                Interlocked.Exchange(ref _isSending, 0);
                if (!_sendQueue.IsEmpty) TryFlushSendQueue();
                return;
            }

            NetworkStream stream = _stream;
            if (stream == null) {
                Interlocked.Exchange(ref _isSending, 0);
                return;
            }

            try {
                stream.BeginWrite(next, 0, next.Length, SendCallback, null);
            }
            catch (ObjectDisposedException) { Interlocked.Exchange(ref _isSending, 0); }
            catch (Exception ex) {
                Debug.LogException(ex);
                Interlocked.Exchange(ref _isSending, 0);
                TriggerDisconnect();
            }
        }

        private void SendCallback(IAsyncResult result) {
            try {
                _stream?.EndWrite(result);
            }
            catch (ObjectDisposedException) { Interlocked.Exchange(ref _isSending, 0); return; }
            catch (Exception ex) {
                Debug.LogException(ex);
                Interlocked.Exchange(ref _isSending, 0);
                TriggerDisconnect();
                return;
            }

            Interlocked.Exchange(ref _isSending, 0);
            TryFlushSendQueue();
        }

        private void ReceiveCallback(IAsyncResult result) {
            if (_disconnected) return;

            try {
                int byteLength = _stream.EndRead(result);
                if (byteLength <= 0) {
                    TriggerDisconnect();
                    return;
                }

                byte[] data = new byte[byteLength];
                Buffer.BlockCopy(_receiveBuffer, 0, data, 0, byteLength);

                _receivedData.Reset(HandleData(data));
                _stream.BeginRead(_receiveBuffer, 0, dataBufferSize, ReceiveCallback, null);
            }
            catch (ObjectDisposedException) { TriggerDisconnect(); }
            catch (Exception ex) {
                Debug.LogException(ex);
                TriggerDisconnect();
            }
        }

        private bool HandleData(byte[] data) {
            _receivedData.SetBytes(data);

            if (_receivedData.UnreadLength() < 4) return true;

            int packetLength = _receivedData.ReadInt();
            if (packetLength <= 0) return true;

            while (packetLength > 0 && packetLength <= _receivedData.UnreadLength()) {
                byte[] packetBytes = _receivedData.ReadBytes(packetLength);
                DispatchPacket(packetBytes);

                packetLength = 0;
                if (_receivedData.UnreadLength() >= 4) {
                    packetLength = _receivedData.ReadInt();
                    if (packetLength <= 0) return true;
                }
            }

            return packetLength <= 1;
        }

        private void DispatchPacket(byte[] packetBytes) {
            int packetId;
            using (Packet peek = new Packet(packetBytes)) {
                packetId = peek.ReadInt();
            }

            if (_offMainThreadPackets.Contains(packetId)) {
                using Packet packet = new Packet(packetBytes);
                packet.ReadInt();
                if (Server.packetHandlers.TryGetValue(packetId, out var handler))
                    handler(_id, packet);
                else
                    Debug.LogWarning($"[Server TCP] No handler for packet id {packetId}");
            }
            else {
                byte[] captured = packetBytes;
                ThreadManager.ExecuteOnMainThread(() => {
                    using Packet packet = new Packet(captured);
                    int id = packet.ReadInt();
                    if (Server.packetHandlers.TryGetValue(id, out var handler))
                        handler(_id, packet);
                    else
                        Debug.LogWarning($"[Server TCP] No handler for packet id {id}");
                });
            }
        }

        private void TriggerDisconnect() {
            if (_disconnected) return;
            _disconnected = true;
            ThreadManager.ExecuteOnMainThread(() => Server.clients[_id]?.Disconnect());
        }

        public void Disconnect() {
            _disconnected = true;

            try { Socket?.Close(); }
            catch (ObjectDisposedException) { }
            catch (Exception ex) { Debug.LogException(ex); }

            _stream = null;
            _receivedData = null;
            _receiveBuffer = null;
            Socket = null;
        }
    }

    public class UDP {
        public IPEndPoint EndPoint;

        private readonly int _id;

        public UDP(int id) {
            _id = id;
        }

        public void Connect(IPEndPoint endPoint) {
            EndPoint = endPoint;
        }

        public void SendData(Packet packet) {
            Server.SendUDPData(EndPoint, packet);
        }

        public void HandleData(Packet packetData) {
            int packetLength = packetData.ReadInt();
            if (packetLength <= 0 || packetLength > packetData.UnreadLength()) return;

            byte[] packetBytes = packetData.ReadBytes(packetLength);

            int packetId;
            using (Packet peek = new Packet(packetBytes)) {
                packetId = peek.ReadInt();
            }

            if (_offMainThreadPackets.Contains(packetId)) {
                using Packet packet = new Packet(packetBytes);
                packet.ReadInt();
                if (Server.packetHandlers.TryGetValue(packetId, out var handler))
                    handler(_id, packet);
                else
                    Debug.LogWarning($"[Server UDP] No handler for packet id {packetId}");
                return;
            }

            byte[] captured = packetBytes;
            ThreadManager.ExecuteOnMainThread(() => {
                using Packet packet = new Packet(captured);
                int id = packet.ReadInt();
                if (Server.packetHandlers.TryGetValue(id, out var handler))
                    handler(_id, packet);
                else
                    Debug.LogWarning($"[Server UDP] No handler for packet id {id}");
            });
        }

        public void Disconnect() {
            EndPoint = null;
        }
    }

    public void SendIntoGame(string playerName) {
        player = GameManager.Instance.InstantiatePlayer();
        player.Initialize(id, playerName);

        Debug.Log($"Spawned player {player.id}.");

        foreach (Client client in Server.clients.Values) {
            if (client.player == null) continue;
            if (client.id != id) ServerSend.SpawnPlayer(id, client.player);
        }

        foreach (Client client in Server.clients.Values) {
            if (client.player != null) ServerSend.SpawnPlayer(client.id, player);
        }
    }

    public void Disconnect() {
        string label = player != null ? $"{player.username} ({player.id})" : $"Client {id}";
        Debug.Log($"{label} disconnected.");

        ThreadManager.ExecuteOnMainThread(() => {
            if (player != null) {
                UnityEngine.Object.Destroy(player.gameObject);
                player = null;
            }
        });

        tcp.Disconnect();
        udp.Disconnect();
    }
}