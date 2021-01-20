using LiteNetLib;
using LiteNetLib.Utils;
using P2P_Relayer.CLI.Rifts;
using P2P_Relayer.Common;
using System;
using System.Net;
using System.Threading;

namespace P2P_Relayer.CLI
{
    internal class Client : INatPunchListener
    {
        private readonly NetManager _manager;
        private readonly EventBasedNetListener _listener;
        private NetPeer _peer;
        private string _p2pToken = string.Empty;
        private bool _connected2Gateway;

        public NatPunchModule NatPunchModule => _manager.NatPunchModule;
        public IRift Rift;

        public Client()
        {
            _listener = new EventBasedNetListener();
            _connected2Gateway = false;

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
            if (Program.Config.IsHost)
                Rift = new TCPClient(80); //TODO: use config
            else
                Rift = new TCPHost();

            Rift.OnConnection = OnRiftConnection;
            Rift.OnConnectionLost = OnRiftConnectionLost;
            Rift.OnReceive = OnRiftData;
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
        private void OnRiftData(int id, byte[] data)
        {
            //Write message
            NetDataWriter writer = new NetDataWriter();
            writer.Put((byte)Opcodes.EventData);
            writer.Put(id);
            writer.PutBytesWithLength(data);

            //Send message
            _peer?.Send(writer, DeliveryMethod.ReliableOrdered);
        }

        private void OnRiftConnectionLost(int id)
        {
            //Write message
            NetDataWriter writer = new NetDataWriter();
            writer.Put((byte)Opcodes.EventDisconnect);
            writer.Put(id);

            //Send message
            _peer?.Send(writer, DeliveryMethod.ReliableOrdered);
        }

        private void OnRiftConnection(int id)
        {
            //Write message
            NetDataWriter writer = new NetDataWriter();
            writer.Put((byte)Opcodes.EventConnect);
            writer.Put(id);

            //Send message
            _peer?.Send(writer, DeliveryMethod.ReliableOrdered);
        }
        #endregion

        public NetPeer Connect(IPEndPoint endpoint)
        {
            return _manager.Connect(endpoint, "Basic_P2P_TCP_Relayer01");
        }

        public void Disconnect()
        {
            _peer?.Disconnect();
        }

        private void ConnectionRequestEvent(ConnectionRequest request)
        {
            if (_peer == null)
                request.Accept();
        }

        private void PeerConnectedEvent(NetPeer peer)
        {
            if (!_connected2Gateway)
            {
                _connected2Gateway = true;
                Console.WriteLine($"Connected to gateway #{peer.Id}.");

                //Create message
                NetDataWriter writer = new NetDataWriter();
                writer.Put((byte)Opcodes.ActivateReq);
                writer.Put(Program.Config.IsHost);
                writer.Put(Program.Config.Token);

                //Send message
                peer.Send(writer, DeliveryMethod.ReliableOrdered);
            }
            else
            {
                Console.WriteLine($"Connected to peer #{peer.Id}.");
                _peer = peer;
            }
        }

        private void PeerDisconnectedEvent(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            if (!_connected2Gateway)
                Console.WriteLine("Gateway is offline!");
            else if (_peer == null)
                Console.WriteLine("Disconnected from gateway.");
            else
                Console.WriteLine($"Disconnected from #{peer.Id}.");

            _peer = null;
        }

        private void NetworkReceiveEvent(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
        {
            MessageHandler.Handle(this, reader);
        }

        public void OnNatIntroductionRequest(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, string token)
        {
            //Client doesn't handle OnNatIntroductionRequest, it's server's job
        }

        public void OnNatIntroductionSuccess(IPEndPoint targetEndPoint, string token)
        {
            if (_peer != null)
                return;

            //Directly connect, if it fails return
            var peer = Connect(targetEndPoint);
            if (peer == null)
                return;

            _peer = peer;
        }
    }
}
