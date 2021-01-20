using P2P_Relayer.Common;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace P2P_Relayer.CLI.Rifts
{
    class TCPHost : IRift
    {
        private class RiftClient
        {
            public int Id;
            public TcpClient TcpClient;
            public TCPHost Host;

            public RiftClient(int id, TcpClient client, TCPHost host)
            {
                Id = id;
                TcpClient = client;
                Host = host;
                new Thread(new ThreadStart(Receive)).Start();
            }

            bool terminated = false;
            public void Terminate()
            {
                terminated = true;
                TcpClient.Close();
                TcpClient = null;
            }

            void Receive()
            {
                try
                {
                    byte[] data = new byte[8192];
                    while (TcpClient != null)
                    {
                        int length = TcpClient.Client.Receive(data);
                        //Soft shutdown
                        if (length == 0)
                            break;
                        var resizedData = new byte[length];
                        Array.Copy(data, resizedData, length);
                        Host.OnReceive?.Invoke(Id, resizedData);
                    }
                }
                catch
                {
                    //Don't handle the error, we are done
                }

                Host.Ids.Free(Id);
                if (!terminated)
                {
                    Console.WriteLine("Host has lost a rift connection");
                    //Connection lost
                    Host.OnConnectionLost?.Invoke(Id);
                }
            }
        }
        public Action<int, byte[]> OnReceive { get; set; }
        public Action<int> OnConnectionLost { get; set; }
        public Action<int> OnConnection { get; set; }

        TcpListener listener;
        readonly IdGenerator Ids = new IdGenerator();
        readonly ConcurrentDictionary<int, RiftClient> Clients = new ConcurrentDictionary<int, RiftClient>();

        public bool IsTcp => true;
        public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;

        public TCPHost()
        {
            listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();

            //Start accepting clients on a new thread
            new Thread(new ThreadStart(AcceptClients)).Start();
        }

        void AcceptClients()
        {
            try
            {
                while (listener != null)
                {
                    var id = Ids.Get();
                    var client = new RiftClient(id, listener.AcceptTcpClient(), this);
                    Console.WriteLine("Host has a new rift connection");
                    Clients.TryAdd(id, client);
                    OnConnection?.Invoke(id);
                }
            }
            catch
            {
                //We don't care about the error, rift's job is over
            }
        }

        public void Send(int id, byte[] data)
        {
            if (Clients.TryGetValue(id, out var target))
            {
                target.TcpClient.Client.Send(data);
            }
        }

        public void Stop()
        {
            foreach (var client in Clients.Values)
                client.Terminate();
            Clients.Clear();
            listener.Stop();
            listener = null;
        }

        public void Disconnect(int id)
        {
            if (Clients.TryRemove(id, out var client))
                client.Terminate();
        }

        public void Connect(int id)
        {
            //Not used, Host can't initiate connections
        }
    }
}
