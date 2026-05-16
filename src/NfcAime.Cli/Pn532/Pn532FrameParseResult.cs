namespace Nfcaime.Cli.Pn532;

internal sealed class Pn532FrameParseResult
{
    public required Pn532FrameKind Kind { get; init; }
    public byte Tfi { get; init; }
    public byte[] Payload { get; init; } = Array.Empty<byte>();
    public byte[]? RawFrame { get; init; }
    public string? Error { get; init; }
}
