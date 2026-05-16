using System;

namespace NfcAime.Dll.PN532 {
    internal static class Pn532FrameParser
    {
        public static Pn532FrameParseResult Parse(ReadOnlySpan<byte> frame)
        {
            if (frame.Length < 6)
            {
                return new Pn532FrameParseResult { Kind = Pn532FrameKind.Invalid, Error = "Frame too short", RawFrame = frame.ToArray() };
            }

            var ack = new ReadOnlySpan<byte>(Pn532HsuFrame.AckFrame);
            var nak = new ReadOnlySpan<byte>(Pn532HsuFrame.NakFrame);

            bool isAck = frame.Length >= 6 && frame.Slice(0, 6).SequenceEqual(ack);
            bool isNak = frame.Length >= 6 && frame.Slice(0, 6).SequenceEqual(nak);

            if (!isAck && !isNak)
            {
                if (frame[0] != Pn532HsuFrame.Preamble || frame[1] != Pn532HsuFrame.StartCode1 || frame[2] != Pn532HsuFrame.StartCode2)
                {
                    return new Pn532FrameParseResult { Kind = Pn532FrameKind.Invalid, Error = "Missing preamble/start codes", RawFrame = frame.ToArray() };
                }
            }

            if (isAck)
            {
                return new Pn532FrameParseResult { Kind = Pn532FrameKind.Ack, RawFrame = frame.ToArray() };
            }

            if (isNak)
            {
                return new Pn532FrameParseResult { Kind = Pn532FrameKind.Nak, RawFrame = frame.ToArray() };
            }

            var length = frame[3];
            var lcs = frame[4];
            if (((length + lcs) & 0xFF) != 0)
            {
                return new Pn532FrameParseResult { Kind = Pn532FrameKind.ChecksumError, Error = "LCS checksum mismatch", RawFrame = frame.ToArray() };
            }

            if (length == 0)
            {
                return new Pn532FrameParseResult { Kind = Pn532FrameKind.Invalid, Error = "Invalid length", RawFrame = frame.ToArray() };
            }

            var expectedLength = 7 + length;
            if (frame.Length < expectedLength)
            {
                return new Pn532FrameParseResult { Kind = Pn532FrameKind.Invalid, Error = "Frame length mismatch", RawFrame = frame.ToArray() };
            }

            var tfi = frame[5];
            var payloadLength = length - 1;

            var payload = payloadLength == 0 ? Array.Empty<byte>() : frame.Slice(6, payloadLength).ToArray();
            var dcs = frame[6 + payloadLength];
            var sum = tfi;
            for (var index = 0; index < payload.Length; index++)
            {
                sum += payload[index];
            }

            if (((sum + dcs) & 0xFF) != 0)
            {
                return new Pn532FrameParseResult { Kind = Pn532FrameKind.ChecksumError, Error = "DCS checksum mismatch", RawFrame = frame.ToArray() };
            }

            if (frame[7 + payloadLength] != Pn532HsuFrame.Postamble)
            {
                return new Pn532FrameParseResult { Kind = Pn532FrameKind.Invalid, Error = "Missing postamble", RawFrame = frame.ToArray() };
            }

            return new Pn532FrameParseResult
            {
                Kind = Pn532FrameKind.Data,
                Tfi = tfi,
                Payload = payload,
                RawFrame = frame.Slice(0, expectedLength).ToArray()
            };
        }
    }
}
