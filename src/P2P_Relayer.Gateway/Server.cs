using LiteNetLib;
using LiteNetLib.Utils;
using P2P_Relayer.Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;

namespace P2P_Relayer.Gateway
{
    internal class Server : INatPunchListener
    {
        private NetManager _manager;
        private readonly EventBasedNetListener _listener;

        private readonly ConcurrentDictionary<string, NetPeer> _hosts = new ConcurrentDictionary<string, NetPeer>();
        private readonly ConcurrentDictionary<string, WaitingPeer> _waitingPeers = new ConcurrentDictionary<string, WaitingPeer>();

        public Server(int port)
        {
            //Set up for networking
            _listener = new EventBasedNetListener();
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
            _manager.Start(port);

            //Start ticking thread
            new Thread(new ThreadStart(ServerTick)).Start();
        }

        public void Stop()
        {
            _manager.Stop();
            _manager = null;
        }

        private void ConnectionRequestEvent(ConnectionRequest request)
        {
            request.AcceptIfKey(Constants.ConnectionKey);
        }

        private void PeerConnectedEvent(NetPeer peer)
        {
            Console.WriteLine($"Peer {peer.Id} has connected.");
        }

        private void PeerDisconnectedEvent(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            //Remove if this was a host
            if (_hosts.Any(x => x.Value.Id == peer.Id))
            {
                _hosts.Remove(_hosts.First(x => x.Value.Id == peer.Id).Key, out _);
                Console.WriteLine($"Host {peer.Id} has disconnected.");
            }
            else
                Console.WriteLine($"Peer {peer.Id} has disconnected.");
        }

        private void NetworkReceiveEvent(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
        {
            if (!reader.TryGetByte(out byte rawOpcode))
                return;

            if (!Enum.IsDefined(typeof(Opcodes), rawOpcode))
                return;

            if ((Opcodes)rawOpcode != Opcodes.ActivateReq)
                return;

            if (!reader.TryGetBool(out var ishost))
                return;

            if (!reader.TryGetString(out var token))
                return;

            //Add host to dictionary or connect peer with host
            if (ishost)
            {
                _hosts.AddOrUpdate(token, peer, (key, old) => peer);
                Console.WriteLine($"Peer {peer.Id} is now a Host.");
            }
            else if (_hosts.TryGetValue(token, out var host))
            {
                //Create message
                var p2ptoken = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
                NetDataWriter writer = new NetDataWriter();
                writer.Put((byte)Opcodes.ActivateAck);
                writer.Put(p2ptoken);

                //Send message
                peer.Send(writer.Data, DeliveryMethod.ReliableOrdered);
                host.Send(writer.Data, DeliveryMethod.ReliableOrdered);
            }
            else
            {
                peer.Send(new byte[] { (byte)Opcodes.ActivateAck }, DeliveryMethod.ReliableOrdered);
            }
        }

        private void ServerTick()
        {
            while (_manager != null)
            {
                _manager?.NatPunchModule.PollEvents();
                Thread.Sleep(50);
            }
        }

        public void OnNatIntroductionRequest(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, string token)
        {
            if (!_waitingPeers.TryGetValue(token, out var wpeer))
            {
                //There is no waiting peer, add into dictionary
                _waitingPeers.TryAdd(token, new WaitingPeer(localEndPoint, remoteEndPoint));
                return;
            }

            if (wpeer.InternalAddr.Equals(localEndPoint) && wpeer.ExternalAddr.Equals(remoteEndPoint))
            {
                wpeer.Refresh();
                return;
            }

            //Introduce client and host to eachother & remove from dictionary
            _manager.NatPunchModule.NatIntroduce(wpeer.InternalAddr, wpeer.ExternalAddr, localEndPoint, remoteEndPoint, token);
            _waitingPeers.TryRemove(token, out _);
        }

        public void OnNatIntroductionSuccess(IPEndPoint targetEndPoint, string token)
        {
            //Server doesn't handle OnNatIntroductionRequest, it's client's job
        }
    }
}
