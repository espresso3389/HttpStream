using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;

namespace HttpStream
{
    /// <summary>
    /// A <see cref="Stream"/> implementation, which provides caching mechanism for slow streams.
    /// </summary>
    public abstract class CacheStream : Stream
    {
        public CacheStream(Stream cacheStream, bool ownStream)
        {
            if (cacheStream == null)
            {
                cacheStream = new MemoryStream();
                ownStream = true;
            }

            if (!cacheStream.CanSeek || !cacheStream.CanRead || !cacheStream.CanWrite)
                throw new ArgumentException("cacheStream should be seekable/readable/writeable.", "cacheStream");

            _cacheStream = cacheStream;
            _ownStream = ownStream;
        }

        Stream _cacheStream;
        bool _ownStream;
        List<Range> _ranges = new List<Range>();

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                if (_ownStream && _cacheStream != null)
                {
                    _cacheStream.Dispose();
                    _cacheStream = null;
                }
            }
        }

        #region implemented abstract members of Stream

        public override void Flush()
        {
            FlushAsync().Wait();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return ReadAsync(buffer, offset, count).Result;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    Position = offset;
                    break;
                case SeekOrigin.Current:
                    Position += offset;
                    break;
                case SeekOrigin.End:
                    Position = Length + offset;
                    break;
            }
            return Position;
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override bool CanRead { get { return true; } }

        public override bool CanSeek { get { return true; } }

        public override bool CanWrite { get { return false; } }

        public override long Length { get { return GetStreamLengthOrDefault(long.MaxValue); } }

        public override long Position { get; set; }

        #endregion

        /// <summary>
        /// Get the length of the stream.
        /// </summary>
        /// <param name="defValue">The value to be returned if the actual length is not available.</param>
        /// <returns>The length of the stream.</returns>
        public abstract long GetStreamLengthOrDefault(long defValue);

        /// <summary>
        /// Load a portion of file and write to a stream.
        /// </summary>
        /// <param name="stream">Stream to write on.</param>
        /// <param name="rangeToLoad">Byte range in the file to load.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The byte range actually loaded. It may be larger than the requested range.</returns>
        public abstract Task<Range> LoadAsync(Stream stream, Range rangeToLoad, CancellationToken cancellationToken);

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (count == 0)
                return 0;

            //Debug.WriteLine(string.Format("Read: Position={0}, Size={1}", Position, count));

            var end = Position + count;

            var idx = _ranges.BinarySearch(new Range(Position, count));
            if (idx <= 0)
                idx = ~idx;

            var firstRange = idx;
            if (idx > 0)
            {
                var prev = _ranges[idx - 1];
                if (prev.Offset <= Position && Position < prev.End)
                {
                    firstRange = --idx;
                }
            }

            int allBytes2Read = count;
            int bytesRead = 0;
            for (;;)
            {
                if (idx == _ranges.Count)
                {
                    var r = await LoadAsync(_cacheStream, new Range(Position, allBytes2Read), cancellationToken);
                    updateRenges(firstRange, r);
                    if (!r.Contains(Position))
                        return bytesRead;

                    var bytesRemain = (int)(r.Length - (Position - r.Offset));
                    if (bytesRemain > allBytes2Read)
                        bytesRemain = allBytes2Read;

                    _cacheStream.Position = Position;
                    await _cacheStream.ReadAsync(buffer, offset, bytesRemain, cancellationToken);

                    Position += bytesRemain;
                    bytesRead += bytesRemain;
                    return (int)bytesRead;
                }

                int ret, bytes2Read;
                if (Position < _ranges[idx].Offset)
                {
                    bytes2Read = (int)(_ranges[idx].Offset - Position);
                    var r = await LoadAsync(_cacheStream, new Range(Position, bytes2Read), cancellationToken);
                    updateRenges(firstRange, r);
                    if (r.Contains(Position))
                    {
                        _cacheStream.Position = Position;
                        if (bytes2Read > allBytes2Read)
                            bytes2Read = allBytes2Read;
                        ret = await _cacheStream.ReadAsync(buffer, offset, bytes2Read, cancellationToken);
                    }
                    else
                    {
                        ret = 0;
                    }
                }
                else
                {
                    if (end < _ranges[idx].End)
                        bytes2Read = (int)(end - Position);
                    else
                        bytes2Read = (int)(_ranges[idx].End - Position);

                    _cacheStream.Position = Position;
                    ret = await _cacheStream.ReadAsync(buffer, offset, bytes2Read, cancellationToken);
                    idx++;
                }

                Position += ret;
                bytesRead += ret;
                offset += ret;
                allBytes2Read -= ret;
                if (ret < bytes2Read || allBytes2Read == 0)
                    return bytesRead;
            }
        }

        void updateRenges(int idx, Range r)
        {
            if (r.Length == 0)
                return;

            var cur = idx >= 0 && idx < _ranges.Count ? _ranges[idx] : null;
            var prev = idx - 1 >= 0 && idx - 1 < _ranges.Count ? _ranges[idx - 1] : null;
            if (idx == _ranges.Count)
            {
                if (prev != null && _ranges.Count != 0 && prev.End == r.Offset)
                {
                    prev.Length = r.End - prev.Offset;
                    return;
                }
                _ranges.Add(r);
                return;
            }

            var count = _ranges.Count;
            if (prev != null && prev.End == r.Offset)
            {
                if (r.End < cur.Offset)
                {
                    // PPPPRRRR CCCC
                    prev.Length = r.End - prev.Offset;
                    return;
                }
                else if (r.End == cur.Offset)
                {
                    // PPPPRRRRCCCC
                    prev.Length = cur.End - prev.Offset;
                    _ranges.RemoveAt(idx);
                    return;
                }
                else
                {
                    var end = r.End;
                    int i;
                    for(i = idx + 1; i < count; i++)
                        if (_ranges[i].End > r.End)
                            break;
                    if (i == count)
                        _ranges.RemoveRange(idx, count - idx);
                    else
                    {
                        end = _ranges[i].End;
                        _ranges.RemoveRange(idx, i - idx + 1);
                    }
                    prev.Length = end - prev.Offset;
                    return;
                }
            }

            if (r.End < cur.Offset)
            {
                _ranges.Insert(idx, r);
                return;
            }
            else if (r.End <= cur.End)
            {
                var offset = (r.Offset < cur.Offset) ? r.Offset : cur.Offset;
                cur.Length = cur.End - offset;
                cur.Offset = offset;
                return;
            }
            else // if (r.End > cur.End)
            {
                var offset = (r.Offset < cur.Offset) ? r.Offset : cur.Offset;
                var end = r.End;
                int i;
                for(i = idx + 1; i < count; i++)
                    if (_ranges[i].End > r.End)
                        break;
                if (i == count)
                    _ranges.RemoveRange(idx + 1, count - idx - 1);
                else
                {
                    end = _ranges[i].End;
                    _ranges.RemoveRange(idx + 1, i - idx + 1 - 1);
                }
                _ranges[idx].Offset = offset;
                _ranges[idx].Length = end - offset;
            }
        }

        public override Task FlushAsync(System.Threading.CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, System.Threading.CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}

