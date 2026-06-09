using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

public class Server {
    public static int MaxPlayers { get; set; }
    private static int port { get; set; }
    public static Dictionary<int, Client> clients = new Dictionary<int, Client>();

    public delegate void packetHandler(int fromClient, Packet packet);

    public static Dictionary<int, packetHandler> packetHandlers;

    private static TcpListener _tcpListener;
    private static UdpClient _udpListener;

    public static void Start(int maxPlayers, int port) {
        MaxPlayers = maxPlayers;
        Server.port = port;
        
        InitializeServerData();

        _tcpListener = new TcpListener(IPAddress.Any, Server.port);
        _tcpListener.Start();
        _tcpListener.BeginAcceptTcpClient(TcpConnectCallback, null);
        
        _udpListener = new UdpClient(Server.port);
        _udpListener.BeginReceive(UDPReceiveCallback, null);
        
        Debug.Log($"Server started on port {Server.port}.");
    }

    private static void TcpConnectCallback(IAsyncResult result) {
        TcpClient client = _tcpListener.EndAcceptTcpClient(result);
        _tcpListener.BeginAcceptTcpClient(TcpConnectCallback, null);

        for (int i = 1; i <= MaxPlayers; i++) {
            if (clients[i].tcp.Socket == null) {
                clients[i].tcp.Connect(client);
                return;
            }
        }
        
        Debug.Log($"Incoming connection from {client.Client.RemoteEndPoint} was rejected because the Server is full.");
    }

    private static void UDPReceiveCallback(IAsyncResult result) {
        try {
            IPEndPoint clientEndPoint = new IPEndPoint(IPAddress.Any, 0);
            byte[] data = _udpListener.EndReceive(result, ref clientEndPoint);
            _udpListener.BeginReceive(UDPReceiveCallback, null);

            if (data.Length < 4) {
                return;
            }

            using (Packet packet = new Packet(data)) {
                int clientId = packet.ReadInt();

                if (clientId == 0) {
                    return;
                }

                if (clients[clientId].udp.EndPoint == null) {
                    clients[clientId].udp.Connect(clientEndPoint);
                    return;
                }

                if (clients[clientId].udp.EndPoint.ToString() == clientEndPoint.ToString()) {
                    clients[clientId].udp.HandleData(packet);
                }
            }
        }
        catch (ObjectDisposedException) {
            
        }
        catch (Exception ex) {
            Debug.LogException(ex);
        }
    }
    
    public static void SendUDPData(IPEndPoint clientEndPoint, Packet packet) {
        try {
            if (clientEndPoint != null) {
                _udpListener.BeginSend(packet.ToArray(), packet.Length(), clientEndPoint, null, null);
            }
        }
        catch (Exception ex) {
            Debug.LogException(ex);
        }
    }
    
    private static void InitializeServerData() {
        for (int i = 1; i <= MaxPlayers; i++) {
            clients.Add(i, new Client(i));
        }

        packetHandlers = new Dictionary<int, packetHandler> {
            { (int)ClientPackets.welcomeReceived, ServerHandle.WelcomeReceived },
            { (int)ClientPackets.measureRtt, ServerHandle.MeasureRTT },
            { (int)ClientPackets.syncTick, ServerHandle.SyncTick },
            { (int)ClientPackets.playerInput, ServerHandle.PlayerInput },
        };
    }
    
    public static void Stop() {
        _tcpListener.Stop();
        _udpListener.Close();
    }
}