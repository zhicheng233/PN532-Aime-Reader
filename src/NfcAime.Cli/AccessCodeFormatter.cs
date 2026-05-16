namespace Nfcaime.Cli;

internal static class AccessCodeFormatter
{
    private const int DecryptedByteCount = 16;
    private const int AccessCodeHexLength = 20;

    internal static string ToAccessCodeString(ReadOnlySpan<byte> decryptedBytes)
    {
        if (decryptedBytes.Length != DecryptedByteCount)
        {
            throw new ArgumentException($"Expected {DecryptedByteCount} decrypted bytes.", nameof(decryptedBytes));
        }

        var hex = Convert.ToHexString(decryptedBytes);
        return hex[^AccessCodeHexLength..];
    }
}
