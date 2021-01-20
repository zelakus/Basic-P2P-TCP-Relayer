using LiteNetLib;
using P2P_Relayer.Common;
using System;

namespace P2P_Relayer.CLI
{
    internal class MessageHandler
    {
        internal static void Handle(Client client, NetPacketReader reader)
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
                    //TODO
                    break;
                case Opcodes.EventDisconnect:
                    //TODO
                    break;
                case Opcodes.EventData:
                    //TODO
                    break;
            }
        }

        private static void HandleActivateAck(Client client, NetPacketReader reader)
        {
            if (!reader.TryGetString(out var token))
            {
                Console.WriteLine("No token in HandleActivateAck.");
                return;
            }

            client.NatPunchModule.SendNatIntroduceRequest(null, token);
        }
    }
}
