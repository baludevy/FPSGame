using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

public class Server {
    public static int MaxPlayers { get; set; }
    private static int Port { get; set; }
    public static Dictionary<int, Client> Clients = new Dictionary<int, Client>();

    public delegate void packetHandler(int fromClient, Packet packet);

    public static Dictionary<int, packetHandler> packetHandlers;

    private static TcpListener _tcpListener;
    private static UdpClient _udpListener;

    public static void Start(int maxPlayers, int port) {
        MaxPlayers = maxPlayers;
        Port = port;
        
        InitializeServerData();

        _tcpListener = new TcpListener(IPAddress.Any, Port);
        _tcpListener.Start();
        _tcpListener.BeginAcceptTcpClient(TcpConnectCallback, null);
        
        _udpListener = new UdpClient(Port);
        _udpListener.BeginReceive(UDPReceiveCallback, null);
        
        Debug.Log($"Server started on port {Port}.");
    }

    private static void TcpConnectCallback(IAsyncResult result) {
        TcpClient client = _tcpListener.EndAcceptTcpClient(result);
        _tcpListener.BeginAcceptTcpClient(TcpConnectCallback, null);

        for (int i = 1; i <= MaxPlayers; i++) {
            if (Clients[i].tcp.Socket == null) {
                Clients[i].tcp.Connect(client);
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

                if (Clients[clientId].udp.EndPoint == null) {
                    Clients[clientId].udp.Connect(clientEndPoint);
                    return;
                }

                if (Clients[clientId].udp.EndPoint.ToString() == clientEndPoint.ToString()) {
                    Clients[clientId].udp.HandleData(packet);
                }
            }
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
            Clients.Add(i, new Client(i));
        }

        packetHandlers = new Dictionary<int, packetHandler> {
            { (int)ClientPackets.welcomeReceived, ServerHandle.WelcomeReceived },
        };
    }
    
    public static void Stop() {
        _tcpListener.Stop();
        _udpListener.Close();
    }
}