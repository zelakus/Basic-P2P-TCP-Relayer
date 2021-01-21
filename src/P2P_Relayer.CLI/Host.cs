using LiteNetLib;
using LiteNetLib.Utils;
using P2P_Relayer.CLI.Rifts;
using P2P_Relayer.Common;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;

namespace P2P_Relayer.CLI
{
    internal class Host : INatPunchListener, IClient
    {
        private readonly NetManager _manager;
        private readonly EventBasedNetListener _listener;
        private readonly ConcurrentDictionary<int, string> _p2pTokens = new ConcurrentDictionary<int, string>();
        private readonly ConcurrentDictionary<int, NetPeer> _peers = new ConcurrentDictionary<int, NetPeer>();
        private NetPeer _gateway;

        public NatPunchModule NatPunchModule => _manager.NatPunchModule;
        public IRift Rift { get; set; }

        public Host()
        {
            _listener = new EventBasedNetListener();

            _manager = new NetManager(_listener)
            {
                AutoRecycle = true,
                NatPunchEnabled = true
            };

            _listener.NetworkReceiveEvent += NetworkReceiveEvent;
            _listener.ConnectionRequestEvent += ConnectionRequestEvent;
            _listener.PeerConnectedEvent += PeerConnectedEvent;
            _listener.PeerDisconnectedEvent += PeerDisconnectedEvent;

            _manager.NatPunchModule.Init(this);
            _manager.Start();

            //Start ticking thread
            new Thread(new ThreadStart(ClientTick)).Start();

            //Start local rift (if we have the host then start client and vice versa)
            Rift = new TCPClient(Program.Config.TargetPort)
            {
                OnConnection = OnRiftConnection,
                OnConnectionLost = OnRiftConnectionLost,
                OnReceive = OnRiftData
            };
            Console.WriteLine($"Created local rift on port {Rift.Port}.");
        }

        private void ClientTick()
        {
            while (_manager != null)
            {
                Thread.Sleep(20);
                try
                {
                    _manager?.PollEvents();
                    _manager?.NatPunchModule.PollEvents();
                }
                catch
                {
                    //Nothing
                }
            }
        }

        #region Rift Events
        private void OnRiftData(long id, byte[] data)
        {
            //Write message
            NetDataWriter writer = new NetDataWriter();
            writer.Put((byte)Opcodes.EventData);
            writer.Put((int)(id & 0xFFFFFFFF));
            writer.PutBytesWithLength(data);

            //Send message
            if (_peers.TryGetValue((int)(id>>32), out var peer))
                peer.Send(writer, DeliveryMethod.ReliableOrdered);
        }

        private void OnRiftConnectionLost(long id)
        {
            //Write message
            NetDataWriter writer = new NetDataWriter();
            writer.Put((byte)Opcodes.EventDisconnect);
            writer.Put((int)(id & 0xFFFFFFFF));

            //Send message
            if (_peers.TryGetValue((int)(id >> 32), out var peer))
                peer.Send(writer, DeliveryMethod.ReliableOrdered);
        }

        private void OnRiftConnection(long id)
        {
            //Write message
            NetDataWriter writer = new NetDataWriter();
            writer.Put((byte)Opcodes.EventConnect);
            writer.Put((int)(id & 0xFFFFFFFF));

            //Send message
            if (_peers.TryGetValue((int)(id >> 32), out var peer))
                peer.Send(writer, DeliveryMethod.ReliableOrdered);
        }
        #endregion

        public NetPeer Connect(IPEndPoint endpoint, bool isGateway = false)
        {
            var peer = _manager.Connect(endpoint, Constants.ConnectionKey);
            if (isGateway)
                _gateway = peer;
            return peer;
        }

        private void ConnectionRequestEvent(ConnectionRequest request)
        {
            request.AcceptIfKey(Constants.ConnectionKey);
        }

        private void PeerConnectedEvent(NetPeer peer)
        {
            if (peer != _gateway)
            {
                Console.WriteLine($"Connected to peer #{peer.Id}.");
                return;
            }

            Console.WriteLine($"Connected to gateway.");

            //Create message
            NetDataWriter writer = new NetDataWriter();
            writer.Put((byte)Opcodes.ActivateReq);
            writer.Put(Program.Config.IsHost);
            writer.Put(Program.Config.Token);

            //Send message
            peer.Send(writer, DeliveryMethod.ReliableOrdered);
        }

        private void PeerDisconnectedEvent(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            if (_gateway == peer)
            {
                _gateway = null;
                Console.WriteLine("Disconnected from gateway.");
                return;
            }

            Rift.DisconnectOf(peer.Id);
            _peers.TryRemove(peer.Id, out _);
            _p2pTokens.TryRemove(peer.Id, out _);
            Console.WriteLine($"Disconnected from peer #{peer.Id}.");
        }

        private void NetworkReceiveEvent(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
        {
            MessageHandler.Handle(this, peer, reader);
        }

        public void OnNatIntroductionRequest(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, string token)
        {
            //Client doesn't handle OnNatIntroductionRequest, it's server's job
        }

        public void OnNatIntroductionSuccess(IPEndPoint targetEndPoint, string token)
        {
            //Check if we already connected (incase both local & remote endpoint succeeds)
            if (_p2pTokens.Values.Contains(token))
                return;

            //Directly connect
            var peer = Connect(targetEndPoint);
            if (peer == null)
                return;

            //Register peer
            _p2pTokens.TryAdd(peer.Id, token);
            _peers.TryAdd(peer.Id, peer);
        }
    }
}
