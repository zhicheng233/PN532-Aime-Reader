namespace Nfcaime.Cli.Pn532;

internal interface IPn532FrameTransport : IDisposable
{
    void Open();
    void Close();
    void WriteFrame(ReadOnlySpan<byte> frame);
    Pn532FrameParseResult ReadFrame(TimeSpan timeout);
}
