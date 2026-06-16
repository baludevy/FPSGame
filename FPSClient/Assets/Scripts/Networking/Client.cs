using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

public class Client : MonoBehaviour {
    public static Client Instance;

    private static int dataBufferSize = 4096;

    public string ip = "0.0.0.0";
    public int port = 42069;

    public int myId;

    public TCP tcp;
    public UDP udp;

    public static volatile bool IsConnected;

    private delegate void PacketHandler(Packet packet);

    private static Dictionary<int, PacketHandler> packetHandlers;

    private void Awake() {
        Instance = this;
    }

    private void Start() {
        tcp = new TCP();
    }

    private void OnApplicationQuit() {
        Disconnect();
    }

    public void ConnectToServer(string ip) {
        this.ip = ip;

        InitializeClientData();
        tcp.Connect();
        udp = new UDP();
    }

    public class TCP {
        public TcpClient socket;

        private NetworkStream _stream;
        private Packet _receivedData;
        private byte[] _receiveBuffer;

        public void Connect() {
            socket = new TcpClient {
                ReceiveBufferSize = dataBufferSize,
                SendBufferSize = dataBufferSize
            };

            _receiveBuffer = new byte[dataBufferSize];
            socket.BeginConnect(Instance.ip, Instance.port, ConnectCallback, socket);
        }

        private void ConnectCallback(IAsyncResult result) {
            socket.EndConnect(result);

            if (!socket.Connected) {
                return;
            }

            IsConnected = true;

            _stream = socket.GetStream();

            _receivedData = new Packet();

            _stream.BeginRead(_receiveBuffer, 0, dataBufferSize, ReceiveCallback, null);
        }

        private void ReceiveCallback(IAsyncResult result) {
            try {
                int byteLength = _stream.EndRead(result);
                if (byteLength <= 0) {
                    Disconnect();
                    return;
                }

                byte[] data = new byte[byteLength];
                Array.Copy(_receiveBuffer, data, byteLength);

                _receivedData.Reset(HandleData(data));
                _stream.BeginRead(_receiveBuffer, 0, dataBufferSize, ReceiveCallback, null);
            }
            catch (ObjectDisposedException) {
            }
            catch (Exception ex) {
                Disconnect();

                Debug.LogException(ex);
            }
        }

        public void SendData(Packet packet) {
            try {
                if (socket != null) {
                    _stream.BeginWrite(packet.ToArray(), 0, packet.Length(), null, null);
                }
            }
            catch (Exception ex) {
                Debug.LogException(ex);
            }
        }

        private bool HandleData(byte[] data) {
            int packetLength = 0;

            _receivedData.SetBytes(data);

            if (_receivedData.UnreadLength() >= 4) {
                packetLength = _receivedData.ReadInt();
                if (packetLength <= 0) {
                    return true;
                }
            }

            while (packetLength > 0 && packetLength <= _receivedData.UnreadLength()) {
                byte[] packetBytes = _receivedData.ReadBytes(packetLength);

                byte[] capturedBytes = packetBytes;

                using (Packet packet = new Packet(capturedBytes)) {
                    int packetId = packet.ReadInt();

                    if (false) {
                        packetHandlers[packetId](packet);

                        ClientHandle.packetsReceived++;
                        ClientHandle.bytesReceived += packet.Length();
                    }
                    else {
                        ThreadManager.ExecuteOnMainThread(() => {
                            using (Packet mainThreadPacket = new Packet(capturedBytes)) {
                                int mainPacketId = mainThreadPacket.ReadInt();
                                if (packetHandlers.TryGetValue(mainPacketId, out PacketHandler handler)) {
                                    handler(mainThreadPacket);
                                    ClientHandle.packetsReceived++;
                                    ClientHandle.bytesReceived += mainThreadPacket.Length();
                                }
                            }
                        });
                    }
                }

                packetLength = 0;
                if (_receivedData.UnreadLength() >= 4) {
                    packetLength = _receivedData.ReadInt();
                    if (packetLength <= 0) {
                        return true;
                    }
                }
            }

            if (packetLength <= 1) {
                return true;
            }

            return false;
        }

        private void Disconnect() {
            Instance.Disconnect();

            _stream = null;
            _receiveBuffer = null;
            _receivedData = null;
            socket = null;
        }
    }

    public class UDP {
        public UdpClient socket;
        public IPEndPoint endPoint;

        public UDP() {
            endPoint = new IPEndPoint(IPAddress.Parse(Instance.ip), Instance.port);
        }

        public void Connect(int localPort) {
            socket = new UdpClient(localPort);

            socket.Connect(endPoint);
            socket.BeginReceive(ReceiveCallback, null);

            using (Packet packet = new Packet()) {
                SendData(packet);
            }
        }

        public void SendData(Packet packet) {
            try {
                packet.InsertInt(Instance.myId);
                if (socket != null) {
                    socket.BeginSend(packet.ToArray(), packet.Length(), null, null);
                }
            }
            catch (Exception ex) {
                Debug.Log(ex);
                Disconnect();
            }
        }

        private void ReceiveCallback(IAsyncResult result) {
            try {
                byte[] data = socket.EndReceive(result, ref endPoint);
                socket.BeginReceive(ReceiveCallback, null);

                if (data.Length < 4) {
                    Instance.Disconnect();
                    return;
                }

                HandleData(data);
            }
            catch (ObjectDisposedException) {
            }
            catch (Exception ex) {
                Debug.LogException(ex);

                // Disconnect();
            }
        }

        private static void HandleData(byte[] data) {
            using (Packet packet = new Packet(data)) {
                int packetLength = packet.ReadInt();
                data = packet.ReadBytes(packetLength);
            }

            using (Packet packet = new Packet(data)) {
                int packetId = packet.ReadInt();

                if (packetId == (int)ServerPackets.gameUpdate) {
                    packetHandlers[packetId](packet);
                    ClientHandle.packetsReceived++;
                    ClientHandle.bytesReceived += packet.Length();
                    return;
                }
            }

            ThreadManager.ExecuteOnMainThread(() => {
                using Packet packet = new Packet(data);

                int packetId = packet.ReadInt();
                packetHandlers[packetId](packet);
                ClientHandle.packetsReceived++;
                ClientHandle.bytesReceived += packet.Length();
            });
        }

        private void Disconnect() {
            Instance.Disconnect();

            endPoint = null;
            socket = null;
        }
    }

    private void InitializeClientData() {
        packetHandlers = new Dictionary<int, PacketHandler>() {
            { (int)ServerPackets.welcome, ClientHandle.Welcome },
            { (int)ServerPackets.syncTick, ClientHandle.SyncTick },
            { (int)ServerPackets.spawnPlayer, ClientHandle.SpawnPlayer },
            { (int)ServerPackets.gameUpdate, ClientHandle.GameUpdate },
        };
    }

    public void Disconnect() {
        if (!IsConnected) return;
        IsConnected = false;

        try {
            tcp?.socket?.Close();
        }
        catch {
        }

        try {
            udp?.socket?.Close();
        }
        catch {
        }

        // disconnect on main thread
        ThreadManager.ExecuteOnMainThread(() => { ConnectionManager.OnDisconnect(); });
    }
}