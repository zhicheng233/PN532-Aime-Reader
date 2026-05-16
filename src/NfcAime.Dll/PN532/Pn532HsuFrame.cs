using System;

namespace NfcAime.Dll.PN532 {
    internal static class Pn532HsuFrame
    {
        public const byte Preamble = 0x00;
        public const byte StartCode1 = 0x00;
        public const byte StartCode2 = 0xFF;
        public const byte Postamble = 0x00;

        public const byte HostToPn532Tfi = 0xD4;
        public const byte Pn532ToHostTfi = 0xD5;

        public static readonly byte[] AckFrame =
        [
            0x00, 0x00, 0xFF, 0x00, 0xFF, 0x00
        ];

        public static readonly byte[] NakFrame =
        [
            0x00, 0x00, 0xFF, 0xFF, 0x00, 0x00
        ];

        public static byte[] BuildDataFrame(byte tfi, ReadOnlySpan<byte> data)
        {
            if (data.Length + 1 > byte.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(data), "PN532 HSU frames must fit in 255 bytes.");
            }

            var length = (byte)(data.Length + 1);
            var lcs = ComputeLcs(length);
            var dcs = ComputeDcs(tfi, data);

            var frame = new byte[7 + length];
            frame[0] = Preamble;
            frame[1] = StartCode1;
            frame[2] = StartCode2;
            frame[3] = length;
            frame[4] = lcs;
            frame[5] = tfi;
            data.CopyTo(frame.AsSpan(6));
            frame[6 + data.Length] = dcs;
            frame[7 + data.Length] = Postamble;

            return frame;
        }

        public static byte ComputeLcs(byte length)
            => (byte)((0x100 - length) & 0xFF);

        public static byte ComputeDcs(byte tfi, ReadOnlySpan<byte> data)
        {
            var sum = tfi;
            for (var index = 0; index < data.Length; index++)
            {
                sum += data[index];
            }

            return (byte)((0x100 - (sum & 0xFF)) & 0xFF);
        }
    }
}
