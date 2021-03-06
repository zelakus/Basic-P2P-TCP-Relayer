﻿using LiteNetLib;
using LiteNetLib.Utils;
using P2P_Relayer.CLI.Rifts;
using P2P_Relayer.Common;
using System;
using System.Net;
using System.Threading;

namespace P2P_Relayer.CLI
{
    internal class Client : INatPunchListener, IClient
    {
        private readonly NetManager _manager;
        private readonly EventBasedNetListener _listener;
        private NetPeer _gateway;
        private NetPeer _peer;

        public NatPunchModule NatPunchModule => _manager.NatPunchModule;
        public IRift Rift { get; set; }

        public Client()
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
            Rift = new TCPHost
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
            writer.Put((int)id);
            writer.PutBytesWithLength(data);

            //Send message
            _peer?.Send(writer, DeliveryMethod.ReliableOrdered);
        }

        private void OnRiftConnectionLost(long id)
        {
            //Write message
            NetDataWriter writer = new NetDataWriter();
            writer.Put((byte)Opcodes.EventDisconnect);
            writer.Put((int)id);

            //Send message
            _peer?.Send(writer, DeliveryMethod.ReliableOrdered);
        }

        private void OnRiftConnection(long id)
        {
            //Write message
            NetDataWriter writer = new NetDataWriter();
            writer.Put((byte)Opcodes.EventConnect);
            writer.Put((int)id);

            //Send message
            _peer?.Send(writer, DeliveryMethod.ReliableOrdered);
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
            if (_peer == null)
                request.AcceptIfKey(Constants.ConnectionKey);
        }

        private void PeerConnectedEvent(NetPeer peer)
        {
            if (peer != _gateway)
            {
                _peer = peer;
                Console.WriteLine($"Connected to host.");
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

            Console.WriteLine($"Disconnected from host.");
            _peer = null;
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
