using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Sledge.Formats
{
    public class ConcatStream : Stream
    {
        private readonly bool _keepOpen;
        private readonly List<Stream> _streams;
        private int _current;
        private long _currentPosition;

        public override bool CanRead { get; }
        public override bool CanSeek { get; }
        public override bool CanWrite => false;
        public override long Length => _streams.Sum(x => x.Length);

        public override long Position
        {
            get
            {
                long p = 0;
                for (var i = 0; i < _current; i++) p += _streams[i].Length;
                p += _currentPosition;
                return p;
            }
            set => Seek(value, SeekOrigin.Begin);
        }

        public ConcatStream(IEnumerable<Stream> streams, bool keepOpen = true)
        {
            _keepOpen = keepOpen;
            _streams = streams.ToList();
            _streams.Add(new MemoryStream(new byte[0])); // EOS

            _current = 0;
            _currentPosition = 0;
            _streams[_current].Seek(_currentPosition, SeekOrigin.Begin);

            CanRead = _streams.All(x => x.CanRead);
            CanSeek = _streams.All(x => x.CanSeek);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = 0;

            while (count > 0)
            {
                if (_current == _streams.Count - 1) break;

                var max = (int)Math.Min(count, _streams[_current].Length - _currentPosition);
                var r = _streams[_current].Read(buffer, offset + read, max);
                count -= r;
                read += r;
                _currentPosition += r;

                // Move to the next stream if needed
                if (_currentPosition == _streams[_current].Length)
                {
                    _current++;
                    _currentPosition = 0;
                    _streams[_current].Seek(0, SeekOrigin.Begin);
                }
            }

            return read;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (origin == SeekOrigin.Current) offset += Position;
            if (origin == SeekOrigin.End) offset = Length - offset;

            foreach (var s in _streams)
            {
                if (s.Length > offset)
                {
                    _currentPosition = offset;
                    s.Seek(offset, SeekOrigin.Begin);
                    break;
                }
                else
                {
                    offset -= s.Length;
                    _current++;
                }
            }

            return Position;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_keepOpen)
            {
                foreach (var s in _streams)
                {
                    s.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
        public override void Flush()
        {
            throw new NotImplementedException();
        }
    }
}