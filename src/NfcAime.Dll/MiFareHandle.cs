using System;

namespace NfcAime.Dll;
using NfcAime.Dll.PN532;
public static class MiFareHandle
{

    public static readonly string[] MifareClassicKeys =
    [
        "6090D00632F5","019761AA8082","574343467632","A99164400748","62742819AD7C","CC5075E42BA1",
        "B9DF35A0814C","8AF9C718F23D","58CD5C3673CB","FC80E88EB88C","7A3CDAD7C023","30424C029001",
        "024E4E44001F","ECBBFA57C6AD","4757698143BD","1D30972E6485","F8526D1A8D6D","1300EC8C7E80",
        "F80A65A87FFA","DEB06ED4AF8E","4AD96BF28190","000390014D41","0800F9917CB0","730050555253",
        "4146D4A956C4","131157FBB126","E69DD9015A43","337237F254D5","9A8389F32FBF","7B8FB4A7100B",
        "C8382A233993","7B304F2A12A6","FC9418BF788B"
    ];

    public static bool TryMifareAuthenticate(
        Pn532Session session,
        byte tg,
        byte blockNumber,
        bool keyTypeA,
        ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> uid4
    )
    {
        var cmd = new byte[2 + 1 + 1 + 6 + 4];
        cmd[0] = 0x40;
        cmd[1] = tg;
        cmd[2] = keyTypeA ? (byte)0x60 : (byte)0x61;
        cmd[3] = blockNumber;
        key.CopyTo(cmd.AsSpan(4, 6));
        uid4.CopyTo(cmd.AsSpan(10, 4));

        var response = session.SendCommand(cmd, TimeSpan.FromMilliseconds(1000));
        if (response.Kind != Pn532FrameKind.Data || response.Payload.Length < 2 || response.Payload[0] != 0x41)
        {
            return false;
        }

        return response.Payload[1] == 0x00;
    }

    public static byte[] ReadMifareBlock(Pn532Session session, byte tg, byte blockNumber)
    {
        var cmd = new byte[] { 0x40, tg, 0x30, blockNumber };
        var response = AimeReader.ExpectPn532ResponseCode(session.SendCommand(cmd, TimeSpan.FromMilliseconds(1200)), expectedResponseCode: 0x41);
        if (response.Payload.Length < 2 || response.Payload[1] != 0x00)
        {
            throw new InvalidOperationException($"Mifare read block failed: status=0x{(response.Payload.Length > 1 ? response.Payload[1] : 0):X2}");
        }

        byte[] data;
        if (response.Payload.Length == 2)
        {
            data = Array.Empty<byte>();
        }
        else
        {
            data = new byte[response.Payload.Length - 2];
            Array.Copy(response.Payload, 2, data, 0, data.Length);
        }

        var result = new byte[16];
        Array.Copy(data, 0, result, 0, 16);
        return result;
    }
}