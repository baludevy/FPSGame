using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

public class Server {
    public static int MaxPlayers { get; private set; }
    public static int Port { get; private set; }

    public static Dictionary<int, Client> clients = new();

    public delegate void PacketHandler(int fromClient, Packet packet);
    public static readonly Dictionary<int, PacketHandler> packetHandlers = new() {
        { (int)ClientPackets.welcomeReceived, ServerHandle.WelcomeReceived },
        { (int)ClientPackets.syncTick,        ServerHandle.SyncTick },
        { (int)ClientPackets.playerInput,     ServerHandle.PlayerInput },
    };

    private static TcpListener _tcpListener;
    private static UdpClient _udpListener;

    private static volatile bool _running;
    
    private static readonly ConcurrentQueue<(IPEndPoint ep, byte[] data)> _udpSendQueue = new();
    private static int _udpSending;

    public static void Start(int maxPlayers, int port) {
        MaxPlayers = maxPlayers;
        Port = port;
        _running = true;

        InitializeServerData();

        _tcpListener = new TcpListener(IPAddress.Any, Port);
        _tcpListener.Start();
        _tcpListener.BeginAcceptTcpClient(TcpConnectCallback, null);

        _udpListener = new UdpClient(Port);
        _udpListener.BeginReceive(UdpReceiveCallback, null);

        Debug.Log($"Server started on port {Port}.");
    }

    private static void TcpConnectCallback(IAsyncResult result) {
        TcpClient client = null;

        try {
            client = _tcpListener.EndAcceptTcpClient(result);
        }
        catch (ObjectDisposedException) { return; }
        catch (Exception ex) { Debug.LogException(ex); }

        if (_running) {
            try {
                _tcpListener.BeginAcceptTcpClient(TcpConnectCallback, null);
            }
            catch (ObjectDisposedException) { return; }
            catch (Exception ex) { Debug.LogException(ex); return; }
        }

        if (client == null) return;

        for (int i = 1; i <= MaxPlayers; i++) {
            if (clients[i].tcp.Socket == null) {
                clients[i].tcp.Connect(client);
                return;
            }
        }

        Debug.Log($"Connection from {client.Client.RemoteEndPoint} rejected - server full.");
        try { client.Close(); } catch { }
    }

    private static void UdpReceiveCallback(IAsyncResult result) {
        IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
        byte[] data = null;

        try {
            data = _udpListener.EndReceive(result, ref remoteEP);
        }
        catch (ObjectDisposedException) { return; }
        catch (SocketException ex) {
            if (ex.SocketErrorCode != SocketError.ConnectionReset)
                Debug.LogException(ex);
        }
        catch (Exception ex) { Debug.LogException(ex); }
        finally {
            if (_running) {
                try { _udpListener.BeginReceive(UdpReceiveCallback, null); }
                catch (ObjectDisposedException) { }
                catch (Exception ex) { Debug.LogException(ex); }
            }
        }

        if (data == null || data.Length < 4) return;

        try {
            using Packet packet = new Packet(data);
            int clientId = packet.ReadInt();

            if (clientId < 1 || clientId > MaxPlayers || !clients.TryGetValue(clientId, out Client c)) return;

            if (c.udp.EndPoint == null) {
                c.udp.Connect(remoteEP);
                return;
            }

            if (c.udp.EndPoint.Equals(remoteEP)) {
                c.udp.HandleData(packet);
            }
        }
        catch (Exception ex) { Debug.LogException(ex); }
    }

    public static void SendUDPData(IPEndPoint clientEndPoint, Packet packet) {
        if (clientEndPoint == null) return;

        byte[] data = packet.ToArray();

        _udpSendQueue.Enqueue((clientEndPoint, data));
        TryFlushUdpSendQueue();
    }

    private static void TryFlushUdpSendQueue() {
        if (Interlocked.CompareExchange(ref _udpSending, 1, 0) != 0) return;

        if (!_udpSendQueue.TryDequeue(out var item)) {
            Interlocked.Exchange(ref _udpSending, 0);
            if (!_udpSendQueue.IsEmpty) TryFlushUdpSendQueue();
            return;
        }

        try {
            _udpListener.BeginSend(item.data, item.data.Length, item.ep, UdpSendCallback, null);
        }
        catch (ObjectDisposedException) { Interlocked.Exchange(ref _udpSending, 0); }
        catch (Exception ex) {
            Debug.LogException(ex);
            Interlocked.Exchange(ref _udpSending, 0);
        }
    }

    private static void UdpSendCallback(IAsyncResult result) {
        try { _udpListener.EndSend(result); }
        catch (ObjectDisposedException) { Interlocked.Exchange(ref _udpSending, 0); return; }
        catch (Exception ex) { Debug.LogException(ex); }

        Interlocked.Exchange(ref _udpSending, 0);
        TryFlushUdpSendQueue();
    }

    private static void InitializeServerData() {
        for (int i = 1; i <= MaxPlayers; i++) {
            if (!clients.ContainsKey(i)) {
                clients.Add(i, new Client(i));
            }
        }
    }

    public static void Stop() {
        _running = false;

        foreach (Client client in clients.Values) {
            try { client?.tcp?.Disconnect(); } catch { }
            try { client?.udp?.Disconnect(); } catch { }
        }

        try { _tcpListener?.Stop(); } catch { }
        try { _udpListener?.Close(); } catch { }
    }
}