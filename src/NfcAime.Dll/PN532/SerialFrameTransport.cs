using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.IO.Ports;
using System.Threading;

namespace NfcAime.Dll.PN532 {
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
            var buffer = new byte[_maxFrameBytes];
            var count = 0;

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
                    if (count < buffer.Length)
                    {
                        buffer[count++] = (byte)next;
                    }

                    if (count >= 6)
                    {
                        var span = new ReadOnlySpan<byte>(buffer, 0, count);
                        for (int i = 0; i <= span.Length - 6; i++)
                        {
                            var window = span.Slice(i);
                            if (window.Length >= 6)
                            {
                                var ack = new ReadOnlySpan<byte>(Pn532HsuFrame.AckFrame);
                                if (window.Slice(0, 6).SequenceEqual(ack))
                                {
                                    return Pn532FrameParser.Parse(ack);
                                }

                                var nak = new ReadOnlySpan<byte>(Pn532HsuFrame.NakFrame);
                                if (window.Slice(0, 6).SequenceEqual(nak))
                                {
                                    return Pn532FrameParser.Parse(nak);
                                }
                            }

                            if (window.Length >= 7 && window[0] == Pn532HsuFrame.Preamble && window[1] == Pn532HsuFrame.StartCode1 && window[2] == Pn532HsuFrame.StartCode2)
                            {
                                var length = window[3];
                                var expectedLength = 7 + length;
                                if (window.Length >= expectedLength)
                                {
                                    return Pn532FrameParser.Parse(window.Slice(0, expectedLength));
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
}
