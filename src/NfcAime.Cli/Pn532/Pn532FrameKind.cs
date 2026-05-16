namespace Nfcaime.Cli.Pn532;

internal enum Pn532FrameKind
{
    Ack,
    Nak,
    Data,
    ChecksumError,
    Invalid
}
