

using System;

namespace NfcAime.Dll.PN532 {
    public interface IPn532FrameTransport : IDisposable
    {
        void Open();
        void Close();
        void WriteFrame(ReadOnlySpan<byte> frame);
        Pn532FrameParseResult ReadFrame(TimeSpan timeout);
    }
}
