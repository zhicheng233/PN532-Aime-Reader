using System.Diagnostics;
using System.Runtime.InteropServices;
using System.IO.Ports;

namespace Nfcaime.Cli.Pn532;

internal sealed class SerialFrameTransport : IPn532FrameTransport
{
    private readonly SerialPort _port;
    private readonly TimeSpan _readChunkTimeout;
    private readonly int _maxFrameBytes;
    private bool _wakeupSent;

    public SerialFrameTransport(string portName, int baud, TimeSpan readChunkTimeout, int maxFrameBytes = 300)
    {
        var chunkTimeout = readChunkTimeout;
        if (chunkTimeout <= TimeSpan.Zero || chunkTimeout > TimeSpan.FromMilliseconds(50))
        {
            chunkTimeout = TimeSpan.FromMilliseconds(50);
        }

        _port = new SerialPort(portName, baud)
        {
            ReadTimeout = (int)chunkTimeout.TotalMilliseconds,
            WriteTimeout = (int)readChunkTimeout.TotalMilliseconds,
            Handshake = Handshake.None,
            Parity = Parity.None,
            DataBits = 8,
            StopBits = StopBits.One,
            DtrEnable = false,
            RtsEnable = false
        };
        _readChunkTimeout = chunkTimeout;
        _maxFrameBytes = maxFrameBytes;
    }

    public void Open()
    {
        if (!_port.IsOpen)
        {
            _port.Open();
            _port.DiscardInBuffer();
            _port.DiscardOutBuffer();
            SendWakeupPattern();
        }
    }

    public void Close()
    {
        if (_port.IsOpen)
        {
            _port.Close();
        }
    }

    public void WriteFrame(ReadOnlySpan<byte> frame)
    {
        if (!_wakeupSent)
        {
            SendWakeupPattern();
        }

        // Clear any stale data from previous commands or wake-up echos
        if (_port.IsOpen && _port.BytesToRead > 0)
        {
            _port.DiscardInBuffer();
        }

        var buffer = frame.ToArray();
        _port.Write(buffer, 0, buffer.Length);
    }

    private void SendWakeupPattern()
    {
        // Simple HSU wakeup: Just long preamble. 
        // We avoid sending SAMConfig here to keep the buffer clean for the first real command.
        var wakeup = new byte[] { 0x55, 0x55, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        _port.Write(wakeup, 0, wakeup.Length);
        Thread.Sleep(50);
        
        if (_port.IsOpen)
        {
            _port.DiscardInBuffer();
        }
        
        _wakeupSent = true;
    }

    public Pn532FrameParseResult ReadFrame(TimeSpan timeout)
    {
        var deadline = Stopwatch.GetTimestamp() + (long)(timeout.TotalSeconds * Stopwatch.Frequency);
        var buffer = new List<byte>(_maxFrameBytes);

        while (Stopwatch.GetTimestamp() < deadline)
        {
            try
            {
                if (_port.BytesToRead == 0)
                {
                    Thread.Sleep(1);
                    continue;
                }

                var next = _port.ReadByte();
                if (next < 0) continue;
                buffer.Add((byte)next);
                
                if (buffer.Count >= 6)
                {
                    var span = CollectionsMarshal.AsSpan(buffer);
                    for (int i = 0; i <= span.Length - 6; i++)
                    {
                        var window = span[i..];
                        if (window.StartsWith(Pn532HsuFrame.AckFrame))
                        {
                            return Pn532FrameParser.Parse(Pn532HsuFrame.AckFrame);
                        }
                        if (window.StartsWith(Pn532HsuFrame.NakFrame))
                        {
                            return Pn532FrameParser.Parse(Pn532HsuFrame.NakFrame);
                        }
                        if (window.Length >= 7 && window[0] == Pn532HsuFrame.Preamble && window[1] == Pn532HsuFrame.StartCode1 && window[2] == Pn532HsuFrame.StartCode2)
                        {
                            var length = window[3];
                            var expectedLength = 7 + length;
                            if (window.Length >= expectedLength)
                            {
                                return Pn532FrameParser.Parse(window[..expectedLength]);
                            }
                        }
                    }
                }
            }
            catch (TimeoutException)
            {
                Thread.Sleep(1);
            }
        }

        return new Pn532FrameParseResult { Kind = Pn532FrameKind.Invalid, Error = "Read timeout" };
    }

    public void Dispose()
    {
        Close();
        _port.Dispose();
    }
}
