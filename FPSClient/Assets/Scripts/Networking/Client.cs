using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

public class Client : MonoBehaviour {
    public static Client Instance;

    private static int dataBufferSize = 4096;

    public string ip = "0.0.0.0";
    public int port = 42069;
    public int myId;

    public TCP tcp;
    public UDP udp;

    public static bool IsConnected;

    private delegate void PacketHandler(Packet packet);

    private static readonly Dictionary<int, PacketHandler> packetHandlers = new() {
        { (int)ServerPackets.welcome, ClientHandle.Welcome },
        { (int)ServerPackets.syncTick, ClientHandle.SyncTick },
        { (int)ServerPackets.spawnPlayer, ClientHandle.SpawnPlayer },
        { (int)ServerPackets.gameUpdate, ClientHandle.GameUpdate },
        { (int)ServerPackets.lagCompVisual, ClientHandle.LagCompVisual },
    };

    private static readonly HashSet<int> _offMainThreadPackets = new() {
        (int)ServerPackets.gameUpdate
    };

    private void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnApplicationQuit() {
        Disconnect();
    }

    public void ConnectToServer(string targetIp) {
        ip = targetIp;
        tcp = new TCP();
        udp = new UDP();
        tcp.Connect();
    }

    public class TCP {
        public TcpClient socket;

        private NetworkStream _stream;
        private Packet _receivedData;
        private byte[] _receiveBuffer;

        private readonly ConcurrentQueue<byte[]> _sendQueue = new();
        private int _isSending;
        private volatile bool _disconnected;

        public void Connect() {
            socket = new TcpClient {
                ReceiveBufferSize = dataBufferSize,
                SendBufferSize = dataBufferSize
            };

            _receiveBuffer = new byte[dataBufferSize];
            _disconnected = false;

            socket.BeginConnect(Instance.ip, Instance.port, ConnectCallback, null);
        }

        private void ConnectCallback(IAsyncResult result) {
            try {
                socket.EndConnect(result);
            }
            catch (Exception ex) {
                Debug.LogException(ex);
                TriggerDisconnect();
                return;
            }

            if (!socket.Connected) {
                TriggerDisconnect();
                return;
            }

            IsConnected = true;
            _stream = socket.GetStream();
            _receivedData = new Packet();

            _stream.BeginRead(_receiveBuffer, 0, dataBufferSize, ReceiveCallback, null);
        }

        public void SendData(Packet packet) {
            if (_disconnected || socket == null) return;

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
            catch (ObjectDisposedException) {
                Interlocked.Exchange(ref _isSending, 0);
            }
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
            catch (ObjectDisposedException) {
                Interlocked.Exchange(ref _isSending, 0);
                return;
            }
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
            catch (ObjectDisposedException) {
                TriggerDisconnect();
            }
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
                if (packetHandlers.TryGetValue(packetId, out var handler)) {
                    handler(packet);
                    ClientHandle.packetsReceived++;
                    ClientHandle.bytesReceived += packet.Length();
                }
                else {
                    Debug.LogWarning($"No handler for packet id {packetId}");
                }
            }
            else {
                byte[] captured = packetBytes;
                ThreadManager.ExecuteOnMainThread(() => {
                    using Packet packet = new Packet(captured);
                    int id = packet.ReadInt();
                    if (packetHandlers.TryGetValue(id, out var handler)) {
                        handler(packet);
                        ClientHandle.packetsReceived++;
                        ClientHandle.bytesReceived += packet.Length();
                    }
                    else {
                        Debug.LogWarning($"No handler for packet id {id}");
                    }
                });
            }
        }

        private void TriggerDisconnect() {
            if (_disconnected) return;
            _disconnected = true;
            ThreadManager.ExecuteOnMainThread(() => Instance.Disconnect());
        }

        public void Disconnect() {
            _disconnected = true;

            try {
                socket?.Close();
            }
            catch (ObjectDisposedException) {
            }
            catch (Exception ex) {
                Debug.LogException(ex);
            }

            _stream = null;
            _receiveBuffer = null;
            _receivedData = null;
            socket = null;
        }
    }

    public class UDP {
        public UdpClient socket;
        public IPEndPoint endPoint;

        private volatile bool _disconnected;

        public UDP() {
            endPoint = new IPEndPoint(IPAddress.Parse(Instance.ip), Instance.port);
        }

        public void Connect(int localPort) {
            socket = new UdpClient(localPort);
            socket.Connect(endPoint);
            _disconnected = false;

            socket.BeginReceive(ReceiveCallback, null);

            using Packet packet = new Packet();
            SendData(packet);
        }

        public void SendData(Packet packet) {
            if (_disconnected || socket == null) return;

            try {
                byte[] payload = packet.ToArray();
                byte[] withId = new byte[payload.Length + 4];
                Buffer.BlockCopy(BitConverter.GetBytes(Instance.myId), 0, withId, 0, 4);
                Buffer.BlockCopy(payload, 0, withId, 4, payload.Length);

                socket.BeginSend(withId, withId.Length, SendCallback, null);
            }
            catch (ObjectDisposedException) {
            }
            catch (Exception ex) {
                Debug.LogException(ex);
                TriggerDisconnect();
            }
        }

        private void SendCallback(IAsyncResult result) {
            try {
                socket?.EndSend(result);
            }
            catch (ObjectDisposedException) {
            }
            catch (Exception ex) {
                Debug.LogException(ex);
            }
        }

        private void ReceiveCallback(IAsyncResult result) {
            if (_disconnected) return;

            byte[] data;
            try {
                data = socket.EndReceive(result, ref endPoint);
            }
            catch (ObjectDisposedException) {
                return;
            }
            catch (Exception ex) {
                Debug.LogException(ex);
                if (!_disconnected)
                    socket?.BeginReceive(ReceiveCallback, null);
                return;
            }

            socket.BeginReceive(ReceiveCallback, null);

            if (data.Length < 4) return;

            HandleData(data);
        }

        private void HandleData(byte[] data) {
            byte[] packetBytes;
            using (Packet wrapper = new Packet(data)) {
                int packetLength = wrapper.ReadInt();
                if (packetLength <= 0 || packetLength > wrapper.UnreadLength()) return;
                packetBytes = wrapper.ReadBytes(packetLength);
            }

            int packetId;
            using (Packet peek = new Packet(packetBytes)) {
                packetId = peek.ReadInt();
            }

            if (_offMainThreadPackets.Contains(packetId)) {
                using Packet packet = new Packet(packetBytes);
                packet.ReadInt();
                if (packetHandlers.TryGetValue(packetId, out var handler)) {
                    handler(packet);
                    ClientHandle.packetsReceived++;
                    ClientHandle.bytesReceived += packet.Length();
                }
                else {
                    Debug.LogWarning($"No handler for packet id {packetId}");
                }

                return;
            }

            byte[] captured = packetBytes;
            ThreadManager.ExecuteOnMainThread(() => {
                using Packet packet = new Packet(captured);
                int id = packet.ReadInt();
                if (packetHandlers.TryGetValue(id, out var handler)) {
                    handler(packet);
                    ClientHandle.packetsReceived++;
                    ClientHandle.bytesReceived += packet.Length();
                }
                else {
                    Debug.LogWarning($"No handler for packet id {id}");
                }
            });
        }

        private void TriggerDisconnect() {
            if (_disconnected) return;
            _disconnected = true;
            ThreadManager.ExecuteOnMainThread(() => Instance.Disconnect());
        }

        public void Disconnect() {
            _disconnected = true;

            try {
                socket?.Close();
            }
            catch (ObjectDisposedException) {
            }
            catch (Exception ex) {
                Debug.LogException(ex);
            }

            endPoint = null;
            socket = null;
        }
    }

    public void Disconnect() {
        IsConnected = false;

        if (tcp != null) {
            tcp.Disconnect();
            tcp = null;
        }
    
        if (udp != null) {
            udp.Disconnect();
            udp = null;
        }

        ThreadManager.ExecuteOnMainThread(() => ConnectionManager.OnDisconnect());
    }
}