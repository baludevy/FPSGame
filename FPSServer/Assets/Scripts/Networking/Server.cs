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
        TcpClient client = null;

        try {
            client = _tcpListener.EndAcceptTcpClient(result);
        }
        catch (ObjectDisposedException) {
            return;
        }
        catch (Exception ex) {
            Debug.LogException(ex);
        }

        try {
            _tcpListener.BeginAcceptTcpClient(TcpConnectCallback, null);
        }
        catch (ObjectDisposedException) {
            return;
        }
        catch (Exception ex) {
            Debug.LogException(ex);
            return;
        }

        if (client == null) return;

        try {
            for (int i = 1; i <= MaxPlayers; i++) {
                if (clients[i].tcp.Socket == null) {
                    clients[i].tcp.Connect(client);
                    return;
                }
            }

            Debug.Log(
                $"Incoming connection from {client.Client.RemoteEndPoint} was rejected because the Server is full.");
            client.Close();
        }
        catch (Exception ex) {
            Debug.LogException(ex);
            try {
                client?.Close();
            }
            catch {
            }
        }
    }

    private static void UDPReceiveCallback(IAsyncResult result) {
        IPEndPoint clientEndPoint = new IPEndPoint(IPAddress.Any, 0);
        byte[] data = null;

        try {
            data = _udpListener.EndReceive(result, ref clientEndPoint);
        }
        catch (ObjectDisposedException) {
            return;
        }
        catch (SocketException ex) {
            if (ex.SocketErrorCode == SocketError.ConnectionReset) {
            }
            else {
                Debug.LogException(ex);
            }
        }
        catch (Exception ex) {
            Debug.LogException(ex);
        }
        finally {
            try {
                _udpListener.BeginReceive(UDPReceiveCallback, null);
            }
            catch (ObjectDisposedException) {
            }
            catch (Exception ex) {
                Debug.LogException(ex);
            }
        }

        if (data == null || data.Length < 4) {
            return;
        }

        try {
            using (Packet packet = new Packet(data)) {
                int clientId = packet.ReadInt();

                if (clientId < 1 || clientId > MaxPlayers || !clients.ContainsKey(clientId)) {
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
            { (int)ClientPackets.syncTick, ServerHandle.SyncTick },
            { (int)ClientPackets.playerInput, ServerHandle.PlayerInput },
        };
    }

    public static void Stop() {
        foreach (Client client in clients.Values) {
            try {
                client?.tcp?.Disconnect();
            }
            catch {
            }

            try {
                client?.udp?.Disconnect();
            }
            catch {
            }
        }

        try {
            _tcpListener?.Stop();
        }
        catch {
        }

        try {
            _udpListener?.Close();
        }
        catch {
        }
    }
}