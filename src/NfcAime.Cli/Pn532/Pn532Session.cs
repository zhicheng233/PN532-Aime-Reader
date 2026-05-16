namespace Nfcaime.Cli.Pn532;

internal sealed class Pn532Session : IDisposable
{
    private readonly IPn532FrameTransport _transport;
    private readonly TimeSpan _timeout;
    private readonly int _maxRetries;

    public Pn532Session(IPn532FrameTransport transport, TimeSpan timeout, int maxRetries)
    {
        _transport = transport;
        _timeout = timeout;
        _maxRetries = maxRetries;
    }

    public void Open() => _transport.Open();

    public void Close() => _transport.Close();

    public void Dispose() => _transport.Dispose();

    public Pn532FrameParseResult SendCommand(ReadOnlySpan<byte> payload)
        => SendCommand(payload, responseTimeout: null);

    public Pn532FrameParseResult SendCommand(ReadOnlySpan<byte> payload, TimeSpan? responseTimeout)
    {
        var responseReadTimeout = responseTimeout ?? _timeout;
        var frame = Pn532HsuFrame.BuildDataFrame(Pn532HsuFrame.HostToPn532Tfi, payload);
        var maxAttempts = _maxRetries + 1;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            _transport.WriteFrame(frame);
            var ack = _transport.ReadFrame(_timeout);
            if (ack.Kind == Pn532FrameKind.Ack)
            {
                var response = _transport.ReadFrame(responseReadTimeout);
                if (response.Kind == Pn532FrameKind.Data)
                {
                    return response;
                }

                if (response.Kind == Pn532FrameKind.ChecksumError)
                {
                    Console.WriteLine($"PN532 checksum error (response); retry {attempt + 1} of {maxAttempts}.");
                    continue;
                }

                if (response.Kind == Pn532FrameKind.Invalid)
                {
                    if (string.Equals(response.Error, "Read timeout", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"PN532 timeout (response after ACK); retry {attempt + 1} of {maxAttempts}.");
                        continue;
                    }

                    Console.WriteLine($"PN532 read error (response): {response.Error}; retry {attempt + 1} of {maxAttempts}.");
                    continue;
                }

                // Unexpected response kind after ACK; retry rather than immediately failing.
                Console.WriteLine($"PN532 unexpected response kind {response.Kind}; retry {attempt + 1} of {maxAttempts}.");
                continue;
            }

            if (ack.Kind == Pn532FrameKind.Nak)
            {
                Console.WriteLine($"PN532 NAK received; retry {attempt + 1} of {maxAttempts}.");
                continue;
            }

            if (ack.Kind == Pn532FrameKind.ChecksumError)
            {
                Console.WriteLine($"PN532 checksum error; retry {attempt + 1} of {maxAttempts}.");
                continue;
            }

            if (ack.Kind == Pn532FrameKind.Invalid)
            {
                if (string.Equals(ack.Error, "Read timeout", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"PN532 timeout (waiting for ACK); retry {attempt + 1} of {maxAttempts}.");
                    continue;
                }

                Console.WriteLine($"PN532 read error: {ack.Error}; retry {attempt + 1} of {maxAttempts}.");
                continue;
            }
        }

        return new Pn532FrameParseResult
        {
            Kind = Pn532FrameKind.Invalid,
            Error = "Retry limit exceeded (no valid ACK/response). Check COM port, baud rate, module mode (HSU/UART), and power."
        };
    }
}
