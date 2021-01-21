using LiteNetLib;
using P2P_Relayer.Common;
using System;
using System.Net;

namespace P2P_Relayer.CLI
{
    internal class MessageHandler
    {
        internal static void Handle(IClient client, NetPeer sender, NetPacketReader reader)
        {
            if (!reader.TryGetByte(out byte rawOpcode))
            {
                Console.WriteLine("No Opcode in message.");
                return;
            }

            if (!Enum.IsDefined(typeof(Opcodes), rawOpcode))
            {
                Console.WriteLine("Raw Opcode is not defined in enum.");
                return;
            }

            switch ((Opcodes)rawOpcode)
            {
                case Opcodes.ActivateAck:
                    HandleActivateAck(client, reader);
                    break;

                case Opcodes.EventConnect:
                    {
                        if (reader.TryGetInt(out var id))
                            client.Rift.Connect(sender.Id, id);
                    }
                    break;
                case Opcodes.EventDisconnect:
                    {
                        if (reader.TryGetInt(out var id))
                            client.Rift.Disconnect(sender.Id, id);
                    }
                    break;
                case Opcodes.EventData:
                    {
                        if (reader.TryGetInt(out var id))
                            if (reader.TryGetBytesWithLength(out var data))
                                client.Rift.Send(sender.Id, id, data);
                    }
                    break;
            }
        }

        private static void HandleActivateAck(IClient client, NetPacketReader reader)
        {
            if (!reader.TryGetString(out var token))
            {
                Console.WriteLine("Host is offline.");
                return;
            }

            client.NatPunchModule.SendNatIntroduceRequest(IPEndPoint.Parse(Program.Config.EndPoint), token);
        }
    }
}
