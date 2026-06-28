using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

public class Server {
    public static int MaxPlayers { get; private set; }
    public static int Port { get; private set; }
    public static Dictionary<int, Client> clients = new();

    public delegate void PacketHandler(int fromClient, Packet packet);

    public static readonly Dictionary<byte, PacketHandler> packetHandlers = new() {
        { (byte)ClientPackets.welcomeReceived, ServerHandle.WelcomeReceived },
        { (byte)ClientPackets.syncTick, ServerHandle.SyncTick },
        { (byte)ClientPackets.playerInput, ServerHandle.PlayerInput },
    };

    private static TcpListener tcpListener;
    private static UdpClient udpListener;
    private static volatile bool running;

    private static readonly object acceptLock = new();

    public static void Start(int maxPlayers, int port) {
        MaxPlayers = maxPlayers;
        Port = port;
        running = true;

        for (int i = 1; i <= MaxPlayers; i++)
            if (!clients.ContainsKey(i))
                clients.Add(i, new Client(i));

        tcpListener = new TcpListener(IPAddress.Any, Port);
        tcpListener.Start();
        tcpListener.BeginAcceptTcpClient(TcpConnectCallback, null);

        udpListener = new UdpClient(Port);
        udpListener.BeginReceive(UdpReceiveCallback, null);

        Debug.Log($"Server started on port {Port}.");
    }

    public static void CheckTimeouts() {
        if (!running) return;
        long now = DateTime.UtcNow.Ticks;
        for (int i = 1; i <= MaxPlayers; i++) {
            Client c = clients[i];
            if (c.tcp.socket == null) continue;
            if (c.TimedOut(now)) {
                Debug.Log($"Client {i} timed out.");
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

        if (running) {
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

        if (incoming == null) return;

        // Locked so two concurrent accepts can't grab the same free slot.
        lock (acceptLock) {
            for (int i = 1; i <= MaxPlayers; i++) {
                if (clients[i].tcp.socket == null) {
                    clients[i].tcp.Connect(incoming);
                    return;
                }
            }
        }

        try {
            incoming.Close();
        }
        catch {
        } // server full
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
            if (ex.SocketErrorCode != SocketError.ConnectionReset) Debug.LogException(ex);
        }
        catch (Exception ex) {
            Debug.LogException(ex);
        }
        finally {
            if (running) {
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

        if (data == null) return;
        int length = data.Length;

        try {
            if (length < 8 || length > NetProtocol.maxPacketSize + 8) return;

            int clientId = BitConverter.ToInt32(data, 0);
            uint magic = BitConverter.ToUInt32(data, 4);
            if (clientId < 1 || clientId > MaxPlayers || magic != NetProtocol.magic ||
                !clients.TryGetValue(clientId, out Client c))
                return;

            IPEndPoint bound = c.udp.GetEndPoint();

            if (bound == null) {
                // First contact must carry a valid token before we pin the endpoint.
                if (length < 8 + NetProtocol.tokenLength) return;
                byte[] token = new byte[NetProtocol.tokenLength];
                Buffer.BlockCopy(data, 8, token, 0, NetProtocol.tokenLength);
                if (!NetProtocol.TokensEqual(c.sessionToken, token)) return;
                c.udp.TryBind(remoteEp); // atomic; a concurrent dup just no-ops
                c.MarkActivity();
                return;
            }

            if (!bound.Equals(remoteEp)) return; // spoofed source endpoint

            c.MarkActivity();
            using Packet packet = new Packet(data, length);
            packet.ReadInt();  // clientId
            packet.ReadUInt(); // magic
            c.udp.HandleData(packet);
        }
        catch (Exception ex) {
            Debug.LogException(ex);
        }
    }

    public static void SendUdpData(IPEndPoint endPoint, Packet packet) {
        if (endPoint == null) return;
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
        running = false;
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
    }
}