using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

public class Client {
    private static int dataBufferSize = NetworkSettings.dataBufferSize;

    public int id;
    public Player player;
    public TCP tcp;
    public UDP udp;

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

            _stream.BeginRead(_receiveBuffer, 0, dataBufferSize, ReceiveCallback, null);
            ServerSend.Welcome(_id);
        }

        public void SendData(Packet packet) {
            try {
                if (Socket != null && _stream != null) {
                    _stream.BeginWrite(packet.ToArray(), 0, packet.Length(), null, null);
                }
            }
            catch (ObjectDisposedException) {
            
            }
            catch (Exception ex) {
                Debug.LogException(ex);
            }
        }

        private void ReceiveCallback(IAsyncResult result) {
            try {
                int byteLength = _stream.EndRead(result);
                if (byteLength <= 0) {
                    Server.clients[_id].Disconnect();
                    return;
                }

                byte[] data = new byte[byteLength];
                Array.Copy(_receiveBuffer, data, byteLength);

                _receivedData.Reset(HandleData(data));
                _stream.BeginRead(_receiveBuffer, 0, dataBufferSize, ReceiveCallback, null);
            }
            catch (ObjectDisposedException) {
                try { Server.clients[_id]?.Disconnect(); } catch { }
            }
            catch (Exception ex) {
                Debug.LogException(ex);
                try { Server.clients[_id]?.Disconnect(); } catch { }
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
            
                using (Packet packet = new Packet(capturedBytes))
                {
                    int packetId = packet.ReadInt();
                    
                    if (packetId == (int)ClientPackets.measureRtt || packetId == (int)ClientPackets.syncTick)
                    {
                        Server.packetHandlers[packetId](_id, packet);
                    }
                    else
                    {
                        ThreadManager.ExecuteOnMainThread(() =>
                        {
                            using Packet p = new Packet(capturedBytes);

                            int id = p.ReadInt();
                            Server.packetHandlers[id](_id, p);
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

            return packetLength <= 1;
        }

        public void Disconnect() {
            try {
                Socket?.Close();
            }
            catch (ObjectDisposedException) { }
            catch (SocketException) { }
            catch (Exception ex) {
                Debug.LogException(ex);
            }

            _stream = null;
            _receivedData = null;
            _receiveBuffer = null;
            Socket = null;
        }
    }

    public class UDP {
        public IPEndPoint EndPoint;

        private int _id;

        public UDP(int id) {
            _id = id;
        }

        public void Connect(IPEndPoint endPoint) {
            EndPoint = endPoint;
        }

        public void SendData(Packet packet) {
            Server.SendUDPData(EndPoint, packet);
        }

        public void HandleData(Packet packetData)
        {
            int packetLength = packetData.ReadInt();
            byte[] packetBytes = packetData.ReadBytes(packetLength);
            byte[] capturedBytes = packetBytes;
            
            using (Packet packet = new Packet(capturedBytes)) {
                int packetId = packet.ReadInt();

                if (packetId == (int)ClientPackets.playerInput) {
                    Server.packetHandlers[packetId](_id, packet);
                    return; 
                }
            }
            
            ThreadManager.ExecuteOnMainThread(() => {
                using (Packet packet = new Packet(capturedBytes)) {
                    int packetId = packet.ReadInt();
                    Server.packetHandlers[packetId](_id, packet);
                }
            });
        }

        public void Disconnect() {
            EndPoint = null;
        }
    }
    
    public void SendIntoGame(string playerName) {
        player = NetworkManager.Instance.InstantiatePlayer();

        player.Initialize(id, playerName);

        Debug.Log($"Spawned player {player.id}.");

        foreach (Client client in Server.clients.Values) {
            if (client.player != null) {
                if (client.id != id) {
                    ServerSend.SpawnPlayer(id, client.player);
                }
            }
        }

        foreach (Client client in Server.clients.Values) {
            if (client.player != null) {
                ServerSend.SpawnPlayer(client.id, player);
            }
        }
    }

    public void Disconnect() {
        if (player != null) {
            Debug.Log($"{player.username} ({player.id}) left.");
        } else {
            Debug.Log($"Client {id} disconnected before spawning.");
        }

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