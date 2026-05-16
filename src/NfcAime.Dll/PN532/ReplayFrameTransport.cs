using System;
using System.Collections.Generic;

namespace NfcAime.Dll.PN532 {
    internal sealed class ReplayFrameTransport : IPn532FrameTransport
    {
        private readonly Queue<byte[]> _frames;
        private bool _injectCorruption;

        public ReplayFrameTransport(IEnumerable<byte[]> frames, bool injectCorruption)
        {
            _frames = new Queue<byte[]>();
            foreach (var frame in frames)
            {
                if (frame == null) continue;
                byte[] copy = new byte[frame.Length];
                Array.Copy(frame, copy, frame.Length);
                _frames.Enqueue(copy);
            }
            _injectCorruption = injectCorruption;
        }

        public void Open()
        {
        }

        public void Close()
        {
        }

        public void WriteFrame(ReadOnlySpan<byte> frame)
        {
        }

        public Pn532FrameParseResult ReadFrame(TimeSpan timeout)
        {
            if (_frames.Count == 0)
            {
                return new Pn532FrameParseResult { Kind = Pn532FrameKind.Invalid, Error = "Replay buffer empty" };
            }

            var frame = _frames.Dequeue();
            if (_injectCorruption)
            {
                _injectCorruption = false;
                if (frame.Length > 0)
                {
                    frame[frame.Length - 1] ^= 0xFF;
                }
            }

            return Pn532FrameParser.Parse(frame);
        }

        public void Dispose()
        {
            _frames.Clear();
        }
    }
}
