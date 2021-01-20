using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace P2P_Relayer.CLI.Rifts
{
    internal class TCPClient : IRift
    {
        private class RiftConnection
        {
            public int Id;
            public TcpClient TcpClient;
            public TCPClient Owner;

            public RiftConnection(int id, TCPClient owner)
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

        public Action<int> OnConnectionLost { get; set; }
        public Action<int> OnConnection { get; set; } //Not used, Client can't receive connection request
        public Action<int, byte[]> OnReceive { get; set; }

        readonly ConcurrentDictionary<int, RiftConnection> Connections = new ConcurrentDictionary<int, RiftConnection>();

        public bool IsTcp => true;
        public int Port => _port;

        public readonly int _port;
        public TCPClient(int port)
        {
            _port = port;
        }

        public void Connect(int id)
        {
            var connection = new RiftConnection(id, this);
            Connections.TryAdd(id, connection);
        }

        public void Disconnect(int id)
        {
            if (Connections.TryRemove(id, out var connection))
                connection.Terminate();
        }

        public void Send(int target, byte[] data)
        {
            try
            {
                if (Connections.TryGetValue(target, out var connection))
                    connection.TcpClient.Client.Send(data);
            }
            catch
            {
                if (Connections.TryGetValue(target, out var connection))
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
