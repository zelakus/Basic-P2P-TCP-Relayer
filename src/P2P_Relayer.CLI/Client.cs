using LiteNetLib;
using LiteNetLib.Utils;
using P2P_Relayer.Common;
using System;
using System.Net;

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


        public Client()
        {
            _listener = new EventBasedNetListener();
            _connected2Gateway = false;

            _manager = new NetManager(_listener)
            {
                AutoRecycle = true,
                UnsyncedEvents = true,
                NatPunchEnabled = true
            };

            _listener.NetworkReceiveEvent += NetworkReceiveEvent;
            _listener.ConnectionRequestEvent += ConnectionRequestEvent;
            _listener.PeerConnectedEvent += PeerConnectedEvent;
            _listener.PeerDisconnectedEvent += PeerDisconnectedEvent;

            _manager.NatPunchModule.Init(this);
            _manager.Start();
        }

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
            _peer = peer;

            if (!_connected2Gateway)
            {
                Console.WriteLine($"Connected to gateway #{peer.Id}.");
                _connected2Gateway = true;

                //Create message
                NetDataWriter writer = new NetDataWriter();
                writer.Put((byte)Opcodes.ActivateReq);
                writer.Put(Program.Config.IsHost);
                writer.Put(Program.Config.Token);

                //Send message
                peer.Send(writer.Data, DeliveryMethod.ReliableOrdered);
            }
            else
                Console.WriteLine($"Connected to peer #{peer.Id}.");
        }

        private void PeerDisconnectedEvent(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            _peer = null;
            Console.WriteLine($"Disconnected from #{peer.Id}.");
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
            if (_p2pToken == token)
                return;

            //Directly connect, if it fails return
            var peer = Connect(targetEndPoint);
            if (peer == null)
                return;

            //If this is not a host then disconnect from gateway, we no longer need it
            _peer.Disconnect();

            // Todo: Create rift depending on config.IsHost

            //Set p2p info
            _p2pToken = token;
            _peer = peer;
        }
    }
}
