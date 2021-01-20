using System;

namespace P2P_Relayer.CLI
{
    internal interface IRift
    {
        Action<int> OnConnectionLost { get; set; }
        Action<int> OnConnection { get; set; }
        void Connect(int id);
        void Disconnect(int id);

        bool IsTcp { get; }
        int Port { get; }

        Action<int, byte[]> OnReceive { get; set; }
        void Send(int id, byte[] data);

        void Stop();
    }
}
