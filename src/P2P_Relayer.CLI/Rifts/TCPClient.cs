using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace P2P_Relayer.CLI.Rifts
{
    internal class TCPClient : IRift
    {
        private class RiftConnection
        {
            public long Id;
            public TcpClient TcpClient;
            public TCPClient Owner;

            public RiftConnection(long id, TCPClient owner)
            {
                Id = id;
                Owner = owner;
                TcpClient = new TcpClient();
                TcpClient.Connect(IPAddress.Loopback, Owner.Port);
                new Thread(new ThreadStart(Receive)).Start();
            }

            bool terminated = false;
            public void Terminate(bool calledOverNetwork = true)
            {
                terminated = calledOverNetwork;
                TcpClient.Close();
                TcpClient = null;
            }

            void Receive()
            {
                try
                {
                    byte[] data = new byte[4096];
                    while (TcpClient != null)
                    {
                        int length = TcpClient.Client.Receive(data);
                        //Soft shutdown
                        if (length == 0)
                            break;
                        var resizedData = new byte[length];
                        Array.Copy(data, resizedData, length);
                        Owner.OnReceive?.Invoke(Id, resizedData);
                    }
                }
                catch
                {
                    //Don't handle the error, we are done
                }

                if (!terminated)
                {
                    //Connection lost
                    Owner.OnConnectionLost?.Invoke(Id);
                }
            }
        }

        public Action<long> OnConnectionLost { get; set; }
        public Action<long> OnConnection { get; set; } //Not used, Client can't receive connection request
        public Action<long, byte[]> OnReceive { get; set; }

        readonly ConcurrentDictionary<long, RiftConnection> Connections = new ConcurrentDictionary<long, RiftConnection>();

        public bool IsTcp => true;
        public int Port => _port;

        public readonly int _port;
        public TCPClient(int port)
        {
            _port = port;
        }

        public void Connect(int ownerId, int id)
        {
            long connectionId = (long)ownerId << 32;
            connectionId |= (long)id;

            var connection = new RiftConnection(connectionId, this);
            Connections.TryAdd(connectionId, connection);
        }

        public void Disconnect(int ownerId, int id)
        {
            long connectionId = (long)ownerId << 32;
            connectionId |= (long)id;

            Disconnect(connectionId);
        }

        private void Disconnect(long id)
        {

            if (Connections.TryRemove(id, out var connection))
                connection.Terminate();
        }

        public void DisconnectOf(int ownerId)
        {
            var keys = Connections.Keys.Where(x => x >> 32 == ownerId);
            foreach (var key in keys)
                Disconnect(key);
        }

        public void Send(int ownerId, int target, byte[] data)
        {
            long connectionId = (long)ownerId << 32;
            connectionId |= (long)target;

            try
            {
                if (Connections.TryGetValue(connectionId, out var connection))
                    connection.TcpClient.Client.Send(data);
            }
            catch
            {
                if (Connections.TryGetValue(connectionId, out var connection))
                    connection.Terminate(false);
            }
        }

        public void Stop()
        {
            foreach (var connection in Connections.Values)
                connection.Terminate(false);
            Connections.Clear();
        }
    }
}
