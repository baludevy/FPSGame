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

    private string ip = "0.0.0.0";
    private int port = 42069;
    public int myId;
    public byte[] sessionToken;

    public TcpConnection tcp;
    public UdpConnection udp;

    private static readonly Dictionary<byte, Action<Packet>> packetHandlers = new() {
        { (byte)ServerPackets.welcome, ClientHandle.Welcome },
        { (byte)ServerPackets.syncTick, ClientHandle.SyncTick },
        { (byte)ServerPackets.spawnPlayer, ClientHandle.SpawnPlayer },
        { (byte)ServerPackets.gameUpdate, ClientHandle.GameUpdate },
        { (byte)ServerPackets.lagCompVisual, ClientHandle.LagCompVisual },
    };


    private static readonly HashSet<byte> offMainThreadPackets = new() {
        (byte)ServerPackets.gameUpdate
    };

    private void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void ConnectToServer(string targetIp) {
        ip = targetIp;
        tcp = new TcpConnection();
        udp = new UdpConnection();
        tcp.Connect();
    }

    public class TcpConnection {
        public TcpClient socket;
        private NetworkStream stream;
        private Packet receivedData;
        private byte[] receiveBuffer;
        private readonly ConcurrentQueue<byte[]> sendQueue = new();
        private int isSending;
        private volatile bool disconnected;

        public void Connect() {
            socket = new TcpClient {
                ReceiveBufferSize = dataBufferSize,
                SendBufferSize = dataBufferSize
            };
            receiveBuffer = new byte[dataBufferSize];
            disconnected = false;
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

            stream = socket.GetStream();
            receivedData = new Packet();
            stream.BeginRead(receiveBuffer, 0, dataBufferSize, ReceiveCallback, null);
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
                    Debug.LogWarning("Buffer cap or oversized packet; disconnecting.");
                    TriggerDisconnect();
                    return true;
                },
                dispatch: bytes =>
                    PacketDispatch.Route(bytes, offMainThreadPackets, packetHandlers));
        }

        private void TriggerDisconnect() {
            if (disconnected) return;
            disconnected = true;
            ThreadManager.ExecuteOnMainThread(() => Instance.Disconnect());
        }

        public void Disconnect() {
            disconnected = true;
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

            stream = null;
            receivedData = null;
            socket = null;
        }
    }

    public class UdpConnection {
        public UdpClient socket;
        public IPEndPoint endPoint;
        private volatile bool disconnected;

        public UdpConnection() {
            endPoint = new IPEndPoint(IPAddress.Parse(Instance.ip), Instance.port);
        }

        public void Connect(int localPort) {
            socket = new UdpClient(localPort);
            socket.Connect(endPoint);
            disconnected = false;
            socket.BeginReceive(ReceiveCallback, null);

            // First datagram carries the token so the server can pin our endpoint.
            byte[] datagram = new byte[8 + NetProtocol.tokenLength];
            try {
                NetProtocol.WriteInt32LE(datagram, 0, Instance.myId);
                NetProtocol.WriteUInt32LE(datagram, 4, NetProtocol.magic);
                Buffer.BlockCopy(Instance.sessionToken, 0, datagram, 8, NetProtocol.tokenLength);
                socket.Send(datagram, datagram.Length);
            }
            catch (Exception ex) {
                Debug.LogException(ex);
            }
        }

        public void SendData(Packet packet) {
            if (disconnected || socket == null) return;
            byte[] payload = packet.ToArray();
            byte[] datagram = new byte[payload.Length + 8];
            try {
                NetProtocol.WriteInt32LE(datagram, 0, Instance.myId);
                NetProtocol.WriteUInt32LE(datagram, 4, NetProtocol.magic);
                Buffer.BlockCopy(payload, 0, datagram, 8, payload.Length);
                socket.Send(datagram, datagram.Length);
            }
            catch (ObjectDisposedException) {
            }
            catch (Exception ex) {
                Debug.LogException(ex);
                TriggerDisconnect();
            }
        }

        private void ReceiveCallback(IAsyncResult result) {
            if (disconnected) return;
            byte[] data = null;
            try {
                IPEndPoint from = new IPEndPoint(IPAddress.Any, 0);
                data = socket.EndReceive(result, ref from);
            }
            catch (ObjectDisposedException) {
                return;
            }
            catch (Exception ex) {
                Debug.LogException(ex);
            }

            if (!disconnected) {
                try {
                    socket.BeginReceive(ReceiveCallback, null);
                }
                catch {
                }
            }

            if (data == null || data.Length < 4 || data.Length > NetProtocol.maxPacketSize + 4)
                return;
            HandleData(data);
        }

        private void HandleData(byte[] data) {
            byte[] packetBytes;
            using (Packet wrapper = new Packet(data, data.Length)) {
                if (wrapper.UnreadLength() < 4) return;
                int packetLength = wrapper.ReadInt();
                if (packetLength <= 0 || packetLength > NetProtocol.maxPacketSize ||
                    packetLength > wrapper.UnreadLength()) return;
                packetBytes = wrapper.ReadBytes(packetLength);
            }

            PacketDispatch.Route(packetBytes, offMainThreadPackets, packetHandlers);
        }

        private void TriggerDisconnect() {
            if (disconnected) return;
            disconnected = true;
            ThreadManager.ExecuteOnMainThread(() => Instance.Disconnect());
        }

        public void Disconnect() {
            disconnected = true;
            try {
                socket?.Close();
            }
            catch {
            }

            endPoint = null;
            socket = null;
        }
    }

    public void Disconnect() {
        if (tcp != null) {
            tcp.Disconnect();
            tcp = null;
        }

        if (udp != null) {
            udp.Disconnect();
            udp = null;
        }

        ThreadManager.ExecuteOnMainThread(() => {
            if (NetworkManager.Instance != null) NetworkManager.Instance.NotifyDisconnected();
        });
    }
}