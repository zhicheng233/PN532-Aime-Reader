namespace Nfcaime.Cli;

internal static class FelicaResponseParser
{
    private const int Spad0Offset = 13;
    private const int Spad0Length = 16;

    internal static byte[] ParseSpad0(ReadOnlySpan<byte> response)
    {
        if (response.Length < Spad0Offset + Spad0Length)
        {
            throw new ArgumentException($"Response must be at least {Spad0Offset + Spad0Length} bytes.", nameof(response));
        }

        return response.Slice(Spad0Offset, Spad0Length).ToArray();
    }
}
