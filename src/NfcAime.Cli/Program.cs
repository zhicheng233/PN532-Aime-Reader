using Nfcaime.Cli.Pn532;
using System.IO.Ports;

namespace Nfcaime.Cli;

internal static class Program
{
    private const string DefaultPort = "COM14";
    private const int DefaultBaud = 115200;
    private const int DefaultRetryCount = 2;
    private const int DefaultTimeoutMs = 1500;

    private static int Main(string[] args)
    {
        if (!TryParseArgs(args, out var options, out var errorMessage))
        {
            Console.Error.WriteLine(errorMessage);
            return 1;
        }

        Console.WriteLine($"Mode: {options.Mode}");
        Console.WriteLine($"Port: {options.Port}");
        Console.WriteLine($"Baud: {options.Baud}");
        Console.WriteLine($"Retries: {options.Retries}");
        Console.WriteLine($"TimeoutMs: {options.TimeoutMs}");

        if (options.Mode == "replay") RunReplay(options);
        else if (options.Mode == "diag") RunDiag(options);
        else RunLive(options);

        return 0;
    }

    private static bool TryParseArgs(string[] args, out CliOptions options, out string errorMessage)
    {
        var parsed = new CliOptions { Port = DefaultPort, Baud = DefaultBaud, Mode = "live" };
        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];
            if (!arg.StartsWith("--", StringComparison.Ordinal)) { errorMessage = $"Unknown argument: {arg}"; options = default!; return false; }
            var equalsIndex = arg.IndexOf('=');
            var name = equalsIndex >= 0 ? arg[..equalsIndex] : arg;
            var value = equalsIndex >= 0 ? arg[(equalsIndex + 1)..] : (index + 1 < args.Length && !args[index + 1].StartsWith("--") ? args[++index] : null);
            switch (name)
            {
                case "--port": parsed.Port = value ?? DefaultPort; break;
                case "--baud": if (int.TryParse(value, out var baud)) parsed.Baud = baud; break;
                case "--mode": parsed.Mode = value?.ToLowerInvariant() ?? "live"; break;
                case "--retries": if (int.TryParse(value, out var retries)) parsed.Retries = retries; break;
                case "--timeoutMs": if (int.TryParse(value, out var timeoutMs)) parsed.TimeoutMs = timeoutMs; break;
                default: return Fail($"Unknown argument: {name}", out options, out errorMessage);
            }
        }
        options = parsed; errorMessage = string.Empty; return true;
    }

    private static bool Fail(string message, out CliOptions options, out string errorMessage)
    {
        options = default!; errorMessage = message; return false;
    }

    private sealed class CliOptions { public string Port { get; set; } = DefaultPort; public int Baud { get; set; } = DefaultBaud; public string Mode { get; set; } = "live"; public string? ReplayFile { get; set; } public bool ReplayCorrupt { get; set; } public int Retries { get; set; } = DefaultRetryCount; public int TimeoutMs { get; set; } = DefaultTimeoutMs; }

    private static void RunLive(CliOptions options)
    {
        var timeout = TimeSpan.FromMilliseconds(options.TimeoutMs);
        using var transport = new SerialFrameTransport(options.Port, options.Baud, timeout);
        using var session = new Pn532Session(transport, timeout, options.Retries);
        session.Open();
        try
        {
            var result = RunPn532FelicaFlow(session);
            Console.WriteLine($"CardId: {Convert.ToHexString(result.CardId)}");
            Console.WriteLine($"AccessCode: {result.AccessCode}");
        }
        finally { session.Close(); }
    }

    private static void RunReplay(CliOptions options)
    {
        var timeout = TimeSpan.FromMilliseconds(options.TimeoutMs);
        var frames = LoadReplayFrames(options.ReplayFile!);
        using var transport = new ReplayFrameTransport(frames, options.ReplayCorrupt);
        using var session = new Pn532Session(transport, timeout, options.Retries);
        session.Open();
        try
        {
            var result = RunPn532FelicaFlow(session);
            Console.WriteLine($"CardId: {Convert.ToHexString(result.CardId)}");
            Console.WriteLine($"AccessCode: {result.AccessCode}");
        }
        finally { session.Close(); }
    }

    private static void RunDiag(CliOptions options)
    {
        using var serial = new SerialPort(options.Port, options.Baud) { ReadTimeout = 500, WriteTimeout = 500 };
        serial.Open();
        serial.Write(new byte[] { 0x55, 0x55, 0x00, 0x00, 0x00 }, 0, 5);
        Thread.Sleep(50);
        var fw = Pn532HsuFrame.BuildDataFrame(Pn532HsuFrame.HostToPn532Tfi, new byte[] { 0x02 });
        serial.Write(fw, 0, fw.Length);
        Thread.Sleep(200);
        var buf = new byte[serial.BytesToRead];
        serial.Read(buf, 0, buf.Length);
        Console.WriteLine($"RX: {Convert.ToHexString(buf)}");
    }

    private enum CardKind { Felica, MifareClassic }
    private sealed record CardTarget(CardKind Kind, byte Tg, byte[] CardId);
    private sealed record FlowResult(byte[] CardId, string AccessCode);

    private static FlowResult RunPn532FelicaFlow(Pn532Session session)
    {
        ExpectPn532ResponseCode(session.SendCommand(new byte[] { 0x02 }), expectedResponseCode: 0x03);
        ExpectPn532StatusOk(session.SendCommand(new byte[] { 0x14, 0x01, 0x14, 0x01 }), expectedResponseCode: 0x15);
        ExpectPn532StatusOk(session.SendCommand(new byte[] { 0x32, 0x01, 0x03 }), expectedResponseCode: 0x33);

        var target = WaitForCard(session);
        Thread.Sleep(100);

        if (target.Kind == CardKind.Felica)
        {
            Console.WriteLine($"Card detected! IDm: {Convert.ToHexString(target.CardId)}");
            var readCmd = FelicaCommandBuilder.BuildReadWithoutEncryptionCommand(target.CardId);
            var readResponse = SendInDataExchange(session, target.Tg, readCmd, TimeSpan.FromSeconds(5));
            var spad0 = FelicaResponseParser.ParseSpad0(readResponse);
            var decryptor = new FeliCaDecryptor();
            var decrypted = decryptor.Decrypt(spad0);
            var accessCode = AccessCodeFormatter.ToAccessCodeString(decrypted);
            return new FlowResult(target.CardId, accessCode);
        }

        Console.WriteLine($"Card detected! TypeA UID: {Convert.ToHexString(target.CardId)}");
        var m1AccessCode = TryReadMifareClassicAccessCode(session, target.Tg, target.CardId);
        if (m1AccessCode is null)
        {
            throw new InvalidOperationException("Failed to read Mifare Classic AccessCode: no key matched sector 0.");
        }

        return new FlowResult(target.CardId, m1AccessCode);
    }

    private static CardTarget WaitForCard(Pn532Session session)
    {
        var warned = false;
        while (true)
        {
            var f212 = session.SendCommand(new byte[] { 0x4A, 0x01, 0x01, 0x00, 0xFF, 0xFF, 0x01, 0x00 }, TimeSpan.FromMilliseconds(500));
            if (ExtractFeliCaTarget(f212, out var target)) return target;

            var a106 = session.SendCommand(new byte[] { 0x4A, 0x01, 0x00 }, TimeSpan.FromMilliseconds(600));
            if (ExtractTypeATarget(a106, out target))
            {
                return target;
            }

            if (!warned) { Console.WriteLine("Waiting for Aime/FeliCa or M1 card..."); warned = true; }
            Thread.Sleep(100);
        }
    }

    private static bool ExtractFeliCaTarget(Pn532FrameParseResult result, out CardTarget target)
    {
        target = null;
        if (result.Kind == Pn532FrameKind.Data && result.Payload.Length >= 15 && result.Payload[0] == 0x4B && result.Payload[1] > 0)
        {
            var tg = result.Payload[2];
            var idm = result.Payload.AsSpan(5, 8).ToArray();
            target = new CardTarget(CardKind.Felica, tg, idm);
            return true;
        }
        return false;
    }

    private static bool ExtractTypeATarget(Pn532FrameParseResult result, out CardTarget target)
    {
        target = null;
        if (result.Kind == Pn532FrameKind.Data && result.Payload.Length >= 8 && result.Payload[0] == 0x4B && result.Payload[1] > 0)
        {
            var tg = result.Payload[2];
            var uidLen = result.Payload[6];
            var uidOffset = 7;
            if (uidLen > 0 && result.Payload.Length >= uidOffset + uidLen)
            {
                var uid = result.Payload.AsSpan(uidOffset, uidLen).ToArray();
                target = new CardTarget(CardKind.MifareClassic, tg, uid);
                return true;
            }
        }
        return false;
    }

    private static string? TryReadMifareClassicAccessCode(Pn532Session session, byte tg, ReadOnlySpan<byte> uid)
    {
        if (uid.Length < 4)
        {
            throw new InvalidOperationException("Mifare Classic UID is shorter than 4 bytes.");
        }

        const byte blockNumber = 2; // sector 0 block 2
        var uid4 = uid[..4];

        foreach (var keyHex in MifareClassicKeys)
        {
            var key = Convert.FromHexString(keyHex);
            if (TryMifareAuthenticate(session, tg, blockNumber, keyTypeA: true, key, uid4) ||
                TryMifareAuthenticate(session, tg, blockNumber, keyTypeA: false, key, uid4))
            {
                var block = ReadMifareBlock(session, tg, blockNumber);
                var hex = Convert.ToHexString(block);
                return hex.Length <= 20 ? hex : hex[^20..];
            }
        }

        return null;
    }

    private static bool TryMifareAuthenticate(
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

    private static byte[] ReadMifareBlock(Pn532Session session, byte tg, byte blockNumber)
    {
        var cmd = new byte[] { 0x40, tg, 0x30, blockNumber };
        var response = ExpectPn532ResponseCode(session.SendCommand(cmd, TimeSpan.FromMilliseconds(1200)), expectedResponseCode: 0x41);
        if (response.Payload.Length < 2 || response.Payload[1] != 0x00)
        {
            throw new InvalidOperationException($"Mifare read block failed: status=0x{(response.Payload.Length > 1 ? response.Payload[1] : 0):X2}");
        }

        var data = response.Payload.Length == 2 ? Array.Empty<byte>() : response.Payload[2..];
        if (data.Length < 16)
        {
            throw new InvalidOperationException($"Mifare read block returned {data.Length} bytes, expected at least 16.");
        }

        return data[..16];
    }

    private static readonly string[] MifareClassicKeys =
    [
        "6090D00632F5","019761AA8082","574343467632","A99164400748","62742819AD7C","CC5075E42BA1",
        "B9DF35A0814C","8AF9C718F23D","58CD5C3673CB","FC80E88EB88C","7A3CDAD7C023","30424C029001",
        "024E4E44001F","ECBBFA57C6AD","4757698143BD","1D30972E6485","F8526D1A8D6D","1300EC8C7E80",
        "F80A65A87FFA","DEB06ED4AF8E","4AD96BF28190","000390014D41","0800F9917CB0","730050555253",
        "4146D4A956C4","131157FBB126","E69DD9015A43","337237F254D5","9A8389F32FBF","7B8FB4A7100B",
        "C8382A233993","7B304F2A12A6","FC9418BF788B"
    ];

    private static byte[] SendInDataExchange(Pn532Session session, byte tg, ReadOnlySpan<byte> payloadToTarget, TimeSpan? timeout = null)
    {
        var pn532Payload = new byte[2 + payloadToTarget.Length];
        pn532Payload[0] = 0x40; pn532Payload[1] = tg;
        payloadToTarget.CopyTo(pn532Payload.AsSpan(2));
        var response = ExpectPn532ResponseCode(session.SendCommand(pn532Payload, responseTimeout: timeout ?? TimeSpan.FromSeconds(4)), expectedResponseCode: 0x41);
        if (response.Payload.Length < 2 || response.Payload[1] != 0x00) 
            throw new InvalidOperationException($"InDataExchange failed: 0x{(response.Payload.Length > 1 ? response.Payload[1] : 0):X2}");
        return response.Payload.Length == 2 ? Array.Empty<byte>() : response.Payload[2..];
    }

    private static Pn532FrameParseResult ExpectPn532ResponseCode(Pn532FrameParseResult response, byte expectedResponseCode)
    {
        if (response.Kind != Pn532FrameKind.Data) throw new InvalidOperationException($"PN532 error: {response.Kind} ({response.Error}).");
        if (response.Payload.Length < 1 || response.Payload[0] != expectedResponseCode) 
            throw new InvalidOperationException($"Expected 0x{expectedResponseCode:X2}, got 0x{(response.Payload.Length > 0 ? response.Payload[0] : 0):X2}");
        return response;
    }

    private static void ExpectPn532StatusOk(Pn532FrameParseResult response, byte expectedResponseCode)
    {
        response = ExpectPn532ResponseCode(response, expectedResponseCode);
        if (response.Payload.Length >= 2 && response.Payload[1] != 0x00) throw new InvalidOperationException($"Status fail: 0x{response.Payload[1]:X2}");
    }

    private static IReadOnlyList<byte[]> LoadReplayFrames(string path) => File.ReadAllLines(path).Where(l => !string.IsNullOrWhiteSpace(l) && !l.Trim().StartsWith('#')).Select(line => line.Split(' ').Select(s => Convert.ToByte(s, 16)).ToArray()).ToList();
}
