using System;
using System.Linq;

namespace NfcAime.Dll;

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

        var bytes = decryptedBytes.ToArray();
        var hex = BitConverter.ToString(bytes).Replace("-", "");
        return hex.Substring(hex.Length - AccessCodeHexLength);
    }

    public static byte[] ToAccessCodeBytes(string accessCode)
    {
        if (string.IsNullOrWhiteSpace(accessCode) || accessCode.Length != AccessCodeHexLength || !IsHexString(accessCode))
        {
            return null;
        }

        return HexStringToBytes(accessCode);
    }

    private static bool IsHexString(string s)
    {
        return s.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'));
    }

    private static byte[] HexStringToBytes(string hex)
    {
        byte[] bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }
        return bytes;
    }
}
