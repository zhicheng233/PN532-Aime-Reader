using System;

namespace NfcAime.Dll;

internal static class FelicaCommandBuilder
{
    internal static byte[] BuildPollingCommand() => new byte[] { 0x06, 0x00, 0x88, 0xB4, 0x01, 0x0F };

    internal static byte[] BuildReadWithoutEncryptionCommand(ReadOnlySpan<byte> idm)
    {
        if (idm.Length != 8)
        {
            throw new ArgumentException("IDm must be exactly 8 bytes.", nameof(idm));
        }

        // FeliCa Read Without Encryption:
        // Byte 0: Length (16 bytes = 0x10)
        // Byte 1: Command Code (0x06)
        // Byte 2-9: IDm
        // Byte 10: Number of Services (0x01)
        // Byte 11-12: Service Code List (0x0B, 0x00 for Service 0x000B, Little Endian)
        // Byte 13: Number of Blocks (0x01)
        // Byte 14-15: Block List (0x80, 0x00 for Service 0, Block 0)
        
        byte[] command = new byte[16];
        command[0] = 0x10; 
        command[1] = 0x06;
        idm.CopyTo(command.AsSpan(2, 8));
        command[10] = 0x01;
        command[11] = 0x0B;
        command[12] = 0x00;
        command[13] = 0x01;
        command[14] = 0x80;
        command[15] = 0x00;
        
        return command;
    }
}
