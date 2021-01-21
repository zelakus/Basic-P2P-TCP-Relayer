using System;

namespace P2P_Relayer.CLI
{
    internal interface IRift
    {
        Action<long> OnConnectionLost { get; set; }
        Action<long> OnConnection { get; set; }
        void Connect(int peerId, int id);
        void Disconnect(int peerId, int id);

        bool IsTcp { get; }
        int Port { get; }

        Action<long, byte[]> OnReceive { get; set; }
        void Send(int peerId, int id, byte[] data);

        void Stop();
        void DisconnectOf(int owner);
    }
}
