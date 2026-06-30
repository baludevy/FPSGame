using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

public class Server {
    public static int maxPlayers { get; private set; }
    public static int port { get; private set; }
    public static Dictionary<int, Client> clients = new();

    public delegate void PacketHandler(int fromClient, Packet packet);

    public static readonly Dictionary<byte, PacketHandler> packetHandlers = new() {
        { (byte)ClientPackets.welcomeReceived, ServerHandle.WelcomeReceived },
        { (byte)ClientPackets.playerInput, ServerHandle.PlayerInput },
    };

    private static TcpListener tcpListener;
    private static UdpClient udpListener;
    private static SafeFlag running = new();

    private static readonly object acceptLock = new();

    public static void Start(int maxPlayers, int port) {
        Server.maxPlayers = maxPlayers;
        Server.port = port;
        running.Set();

        for (int i = 1; i <= Server.maxPlayers; i++) {
            if (!clients.ContainsKey(i)) {
                clients.Add(i, new Client(i));
            }
        }

        tcpListener = new TcpListener(IPAddress.Any, Server.port);
        tcpListener.Start();
        tcpListener.BeginAcceptTcpClient(TcpConnectCallback, null);

        udpListener = new UdpClient(Server.port);
        udpListener.BeginReceive(UdpReceiveCallback, null);

        Debug.Log($"Started on port {Server.port}");
    }

    public static void CheckTimeouts() {
        if (!running.IsSet()) {
            return;
        }

        long now = DateTime.UtcNow.Ticks;
        for (int i = 1; i <= maxPlayers; i++) {
            Client c = clients[i];
            if (c.tcp.socket == null) {
                continue;
            }

            if (c.TimedOut(now)) {
                Debug.Log($"Client {i} timed out");
                c.Disconnect();
            }
        }
    }

    private static void TcpConnectCallback(IAsyncResult result) {
        TcpClient incoming = null;
        try {
            incoming = tcpListener.EndAcceptTcpClient(result);
        }
        catch (ObjectDisposedException) {
            return;
        }
        catch (Exception ex) {
            Debug.LogException(ex);
        }

        if (running.IsSet()) {
            try {
                tcpListener.BeginAcceptTcpClient(TcpConnectCallback, null);
            }
            catch (ObjectDisposedException) {
                return;
            }
            catch (Exception ex) {
                Debug.LogException(ex);
                return;
            }
        }

        if (incoming == null) {
            return;
        }

        lock (acceptLock) {
            for (int i = 1; i <= maxPlayers; i++) {
                if (clients[i].tcp.socket == null) {
                    clients[i].tcp.Connect(incoming);
                    return;
                }
            }
        }

        Debug.Log(
            $"Server full, refusing connection from {IPAddress.Parse(((IPEndPoint)incoming.Client.RemoteEndPoint).Address.ToString())}");
        try {
            incoming.Close();
        }
        catch {
        }
    }

    private static void UdpReceiveCallback(IAsyncResult result) {
        IPEndPoint remoteEp = new IPEndPoint(IPAddress.Any, 0);
        byte[] data = null;

        try {
            data = udpListener.EndReceive(result, ref remoteEp);
        }
        catch (ObjectDisposedException) {
            return;
        }
        catch (SocketException ex) {
            if (ex.SocketErrorCode != SocketError.ConnectionReset) {
                Debug.LogException(ex);
            }
        }
        catch (Exception ex) {
            Debug.LogException(ex);
        }
        finally {
            if (running.IsSet()) {
                try {
                    udpListener.BeginReceive(UdpReceiveCallback, null);
                }
                catch (ObjectDisposedException) {
                }
                catch (Exception ex) {
                    Debug.LogException(ex);
                }
            }
        }

        if (data == null) {
            return;
        }

        int length = data.Length;

        try {
            if (length < 8 || length > NetProtocol.maxPacketSize + 8) {
                return;
            }

            int clientId = BitConverter.ToInt32(data, 0);
            uint magic = BitConverter.ToUInt32(data, 4);
            if (clientId < 1 || clientId > maxPlayers || magic != NetProtocol.magic ||
                !clients.TryGetValue(clientId, out Client client)) {
                return;
            }

            IPEndPoint bound = client.udp.GetEndPoint();

            if (bound == null) {
                if (client.tcp.socket == null) {
                    return;
                }

                if (client.udp.TryBind(remoteEp)) {
                    client.MarkUdpBound();
                    Debug.Log($"Client {clientId} UDP endpoint bound");
                }

                return;
            }

            if (!bound.Equals(remoteEp)) {
                return;
            }

            client.MarkActivity();
            using Packet packet = new Packet(data, length);
            packet.ReadInt();
            packet.ReadUInt();
            client.udp.HandleData(packet);
        }
        catch (Exception ex) {
            Debug.LogException(ex);
        }
    }

    public static void SendUdpData(IPEndPoint endPoint, Packet packet) {
        if (endPoint == null) {
            return;
        }

        try {
            byte[] data = packet.ToArray();
            udpListener.Send(data, data.Length, endPoint);
        }
        catch (ObjectDisposedException) {
        }
        catch (Exception ex) {
            Debug.LogException(ex);
        }
    }

    public static void Stop() {
        running.Clear();

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
            tcpListener?.Stop();
        }
        catch {
        }

        try {
            udpListener?.Close();
        }
        catch {
        }

        Debug.Log("[Server] Stopped");
    }
}