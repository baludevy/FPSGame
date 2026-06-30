using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using PimDeWitte.UnityMainThreadDispatcher;
using UnityEngine;

public class Client : MonoBehaviour {
    public static Client Instance;

    private static int dataBufferSize = 4096;

    private string ip = "0.0.0.0";
    private int tcpPort = 42069;
    private int udpPort = 42069;
    public int myId;
    public byte[] sessionToken;

    [NonSerialized] public TcpConnection tcp;
    [NonSerialized] public UdpConnection udp;

    private static readonly Dictionary<byte, Action<Packet>> packetHandlers = new() {
        { (byte)ServerPackets.welcome, ClientHandle.Welcome },
        { (byte)ServerPackets.udpConfirmed, ClientHandle.UdpConfirmed },
        { (byte)ServerPackets.spawnPlayer, ClientHandle.SpawnPlayer },
        { (byte)ServerPackets.gameUpdate, ClientHandle.GameUpdate },
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

    public void ConnectToServer(string targetIp, int targetTcpPort, int targetUdpPort) {
        ip = targetIp;
        tcpPort = targetTcpPort;
        udpPort = targetUdpPort;
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
        private SendGate sendGate = new();
        private SafeFlag disconnected = new();

        public void Connect() {
            socket = new TcpClient {
                ReceiveBufferSize = dataBufferSize,
                SendBufferSize = dataBufferSize
            };
            receiveBuffer = new byte[dataBufferSize];
            disconnected.Clear();
            socket.BeginConnect(Instance.ip, Instance.tcpPort, ConnectCallback, null);
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
                    Debug.LogWarning("Oversized TCP packet, disconnecting");
                    TriggerDisconnect();
                    return true;
                },
                dispatch: bytes => PacketDispatch.Route(bytes, offMainThreadPackets, packetHandlers));
        }

        private void TriggerDisconnect() {
            if (disconnected.IsSet()) {
                return;
            }

            disconnected.Set();
            UnityMainThreadDispatcher.Instance().Enqueue(() => { Instance.Disconnect(); });
        }

        public void Disconnect() {
            disconnected.Set();

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
        private SafeFlag disconnected = new();
        private Coroutine pingCoroutine;

        public UdpConnection() {
            endPoint = new IPEndPoint(IPAddress.Parse(Instance.ip), Instance.udpPort);
        }

        public void Connect(int localPort) {
            socket = new UdpClient(localPort);
            socket.Connect(endPoint);
            disconnected.Clear();
            socket.BeginReceive(ReceiveCallback, null);

            if (pingCoroutine != null) {
                Instance.StopCoroutine(pingCoroutine);
            }

            pingCoroutine = Instance.StartCoroutine(KeepSendingPing());
        }


        private IEnumerator KeepSendingPing() {
            byte[] datagram = new byte[8];
            NetProtocol.WriteInt32LE(datagram, 0, Instance.myId);
            NetProtocol.WriteUInt32LE(datagram, 4, NetProtocol.magic);

            while (!disconnected.IsSet()) {
                try {
                    socket.Send(datagram, datagram.Length);
                }
                catch (Exception ex) {
                    Debug.LogException(ex);
                }

                yield return new WaitForSecondsRealtime(0.2f);
            }
        }

        public void StopPingRetry() {
            if (pingCoroutine != null) {
                Instance.StopCoroutine(pingCoroutine);
                pingCoroutine = null;
            }
        }

        public void SendData(Packet packet) {
            if (disconnected.IsSet() || socket == null) {
                return;
            }

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
            if (disconnected.IsSet()) {
                return;
            }

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

            if (!disconnected.IsSet()) {
                try {
                    socket.BeginReceive(ReceiveCallback, null);
                }
                catch {
                }
            }

            if (data == null || data.Length < 4 || data.Length > NetProtocol.maxPacketSize + 4) {
                return;
            }

            HandleData(data);
        }

        private void HandleData(byte[] data) {
            byte[] packetBytes;
            using (Packet wrapper = new Packet(data, data.Length)) {
                if (wrapper.UnreadLength() < 4) {
                    return;
                }

                int packetLength = wrapper.ReadInt();
                if (packetLength <= 0 || packetLength > NetProtocol.maxPacketSize ||
                    packetLength > wrapper.UnreadLength()) {
                    return;
                }

                packetBytes = wrapper.ReadBytes(packetLength);
            }

            PacketDispatch.Route(packetBytes, offMainThreadPackets, packetHandlers);
        }

        private void TriggerDisconnect() {
            if (disconnected.IsSet()) {
                return;
            }

            disconnected.Set();
            UnityMainThreadDispatcher.Instance().Enqueue(() => { Instance.Disconnect(); });
        }

        public void Disconnect() {
            disconnected.Set();

            if (pingCoroutine != null) {
                Instance.StopCoroutine(pingCoroutine);
                pingCoroutine = null;
            }

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

        UnityMainThreadDispatcher.Instance().Enqueue(() => {
            if (NetworkManager.Instance != null) {
                NetworkManager.Instance.NotifyDisconnected();
            }
        });
    }
}