namespace P2P_Relayer.Common
{
    public enum Opcodes : byte
    {
        ActivateReq,
        ActivateAck,

        EventConnect,
        EventDisconnect,
        EventData
    }
}
