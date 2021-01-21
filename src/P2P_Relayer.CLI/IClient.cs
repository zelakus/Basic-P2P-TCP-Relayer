using LiteNetLib;
using System.Net;

namespace P2P_Relayer.CLI
{
    internal interface IClient
    {
        NatPunchModule NatPunchModule { get; }
        IRift Rift { get; set; }

        NetPeer Connect(IPEndPoint endPoint, bool isGateway = false);
    }
}
