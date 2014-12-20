using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Globalization;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;

namespace HttpStream
{
    /// <summary>
    /// Implements randomly accessible <see cref="Stream"/> on HTTP 1.1 transport.
    /// </summary>
	public class HttpStream : CacheStream
    {
        Uri _uri;
        HttpClient _httpClient;
        bool _ownHttpClient;
        int _bufferingSize = 64 * 1024;

        /// <summary>
        /// Size in bytes of the file downloaing if available; otherwise it returns <see cref="long.MaxValue"/>.
        /// <seealso cref="FileSizeAvailable"/>
        /// <seealso cref="GetStreamLengthOrDefault"/>
        /// </summary>
        public long FileSize { get; private set; }
        /// <summary>
        /// Whether <see cref="FileSize"/> is available or not.
        /// <seealso cref="FileSize"/>
        /// <seealso cref="GetStreamLengthOrDefault"/>
        /// </summary>
		public bool FileSizeAvailable { get; private set; }
        /// <summary>
        /// Whether file properties, like file size and last modified time is correctly inspected.
        /// </summary>
		public bool InspectionFinished { get; private set; }
        /// <summary>
        /// When the file is last modified.
        /// </summary>
		public DateTime LastModified { get; private set; }
        /// <summary>
        /// Content type of the file.
        /// </summary>
        public string ContentType { get; private set; }
        /// <summary>
        /// Buffering size for downloading the file.
        /// </summary>
		public int BufferingSize
        {
            get { return _bufferingSize; }
            set
            {
                if (value == 0 || bitCount(value) != 1)
                    throw new ArgumentOutOfRangeException("BufferingSize should be 2^n.");
                _bufferingSize = value;
            }
        }

        static int bitCount(int i)
        {
            i = i - ((i >> 1) & 0x55555555);
            i = (i & 0x33333333) + ((i >> 2) & 0x33333333);
            return (((i + (i >> 4)) & 0x0F0F0F0F) * 0x01010101) >> 24;
        }

        /// <summary>
        /// Creates a new HttpStream with the specified URI.
        /// The file will be cached on memory.
        /// </summary>
        /// <param name="uri">URI of the file to download.</param>
        public HttpStream(Uri uri) : this(uri, new MemoryStream(), true)
        {
        }

        /// <summary>
        /// Creates a new HttpStream with the specified URI.
        /// </summary>
        /// <param name="uri">URI of the file to download.</param>
        /// <param name="cache">Stream, on which the file will be cached. It should be seekable, readable and writeable.</param>
        /// <param name="ownStream"><c>true</c> to dispose <paramref name="cache"/> on HttpStream's cleanup.</param>
		public HttpStream(Uri uri, Stream cache, bool ownStream) : this(uri, cache, ownStream, null, null)
        {
        }

        /// <summary>
        /// Creates a new HttpStream with the specified URI.
        /// </summary>
        /// <param name="uri">URI of the file to download.</param>
        /// <param name="cache">Stream, on which the file will be cached. It should be seekable, readable and writeable.</param>
        /// <param name="ownStream"><c>true</c> to dispose <paramref name="cache"/> on HttpStream's cleanup.</param>
        /// <param name="rangesAlreadyCached">File ranges already cached on <paramref name="cacheStream"/> if any; otherwise it can be <c>null</c>.</param>
		public HttpStream(Uri uri, Stream cache, bool ownStream, IEnumerable<Range> rangesAlreadyCached) : this(uri, cache, ownStream, rangesAlreadyCached, null)
        {
        }

        /// <summary>
        /// Creates a new HttpStream with the specified URI.
        /// </summary>
        /// <param name="uri">URI of the file to download.</param>
        /// <param name="cache">Stream, on which the file will be cached. It should be seekable, readable and writeable.</param>
        /// <param name="ownStream"><c>true</c> to dispose <paramref name="cache"/> on HttpStream's cleanup.</param>
        /// <param name="rangesAlreadyCached">File ranges already cached on <paramref name="cacheStream"/> if any; otherwise it can be <c>null</c>.</param>
        /// <param name="httpClient"><see cref="HttpClient"/> to use on creating HTTP requests.</param>
		public HttpStream(Uri uri, Stream cache, bool ownStream, IEnumerable<Range> rangesAlreadyCached, HttpClient httpClient) : base(cache, ownStream, rangesAlreadyCached)
        {
            FileSize = long.MaxValue;
            _uri = uri;
            _httpClient = httpClient;
            if (_httpClient == null)
            {
                _httpClient = new HttpClient();
                _ownHttpClient = true;
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && _ownHttpClient)
            {
                _httpClient.Dispose();
            }
        }

        /// <summary>
        /// Size in bytes of the file downloaing if available.
        /// </summary>
        /// <param name="defValue">If the file is not available, the value is returned.</param>
        /// <seealso cref="FileSize"/>
        /// <seealso cref="FileSizeAvailable"/>
        /// <returns>The file size.</returns>
		public override long GetStreamLengthOrDefault(long defValue)
        {
            return FileSizeAvailable ? FileSize : defValue;
        }

        /// <summary>
        /// Download a portion of file and write to a stream.
        /// </summary>
        /// <param name="stream">Stream to write on.</param>
        /// <param name="rangeToLoad">Byte range in the file to load.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The byte range actually downloaded. It may be larger than the requested range.</returns>
		protected override async Task<Range> LoadAsync(Stream stream, Range rangeToLoad, CancellationToken cancellationToken)
        {
            if (stream == null || rangeToLoad == null)
                throw new ArgumentNullException();
            if (rangeToLoad.Length == 0)
                return rangeToLoad;

            // confirm block unit access
            long lowfilter = BufferingSize - 1;
            long begPos = rangeToLoad.Offset & ~lowfilter;
            long endPos = (rangeToLoad.Offset + rangeToLoad.Length + lowfilter) & ~lowfilter;
            rangeToLoad.Offset &= ~lowfilter;
            if (FileSizeAvailable && endPos > FileSize)
                endPos = FileSize;
            rangeToLoad.Length = (int)(endPos - begPos);

            var req = new HttpRequestMessage(HttpMethod.Get, _uri);
            // Use "Range" header to sepcify the data offset and size
            req.Headers.Add("Range", string.Format("bytes={0}-{1}", begPos, begPos + rangeToLoad.Length - 1));

            // post the request
            var res = await _httpClient.SendAsync(req, cancellationToken);
            if (!res.IsSuccessStatusCode)
                throw new Exception(string.Format("HTTP Status: {0} for bytes={1}-{2}", res.StatusCode, begPos, begPos + rangeToLoad.Length - 1));
            else
                Debug.WriteLine(string.Format("HTTP Status: {0} for bytes={1}-{2}", res.StatusCode, begPos, begPos + rangeToLoad.Length - 1));

            // retrieve the resulting Content-Range
            bool getRanges = true;
            long begin = 0, end = long.MaxValue;
            long size = long.MaxValue;
            if (!actionIfFound(res, "Content-Range", range =>
            {
                // 206
                Debug.WriteLine(range);
                var m = Regex.Match(range, @"bytes\s+([0-9]+)-([0-9]+)/(\w+)");
                begin = long.Parse(m.Groups[1].Value);
                end = long.Parse(m.Groups[2].Value);
                size = end - begin + 1;

                if (!FileSizeAvailable)
                {
                    var sz = m.Groups[3].Value;
                    if (sz != "*")
                    {
                        FileSize = long.Parse(sz);
                        FileSizeAvailable = true;
                    }
                }

                Debug.WriteLine(string.Format("Req: {0}-{1} -> Res: {2} (of {3})", begin, end, size, FileSize));
            }))
            {
                // In some case, there's no Content-Range but Content-Length
                // instead.
                getRanges = false;
                begin = 0;
                actionIfFound(res, "Content-Length", v =>
                {
                    FileSize = end = size = long.Parse(v);
                    FileSizeAvailable = true;
                    Debug.WriteLine("Content-Type: " + ContentType);
                });
            }

            actionIfFound(res, "Content-Type", v =>
            {
                ContentType = v;
                Debug.WriteLine("Content-Type: " + ContentType);
            });

            actionIfFound(res, "Last-Modified", v =>
            {
                LastModified = parseDateTime(v);
                Debug.WriteLine("Last-Modified: " + LastModified.ToString("O"));
            });

            InspectionFinished = true;

            //if(size > 0xffffffffULL)
            //    celThrow2(errOperationFailed, "Data size too large!");

            var s = await res.Content.ReadAsStreamAsync();

            int size32 = (int)size;
            stream.Position = begin;
            var buf = new byte[BufferingSize];
            var copied = 0;
            while (size32 > 0)
            {
                var bytes2Read = Math.Min(size32, BufferingSize);
                var bytesRead = await s.ReadAsync(buf, 0, bytes2Read, cancellationToken);
                if (bytesRead == 0)
                    break;

                await stream.WriteAsync(buf, 0, bytesRead, cancellationToken);
                size32 -= bytesRead;
                copied += bytesRead;
            }

            if (!FileSizeAvailable && !getRanges)
            {
                FileSize = copied;
                FileSizeAvailable = true;
            }

            var r = new Range(begin, copied);
            if (RangeDownloaded != null)
                RangeDownloaded(this, new RangeDownloadedEventArgs { NewlyLoaded = r });
            return r;
        }

        bool actionIfFound(HttpResponseMessage res, string name, Action<string> action)
        {
            IEnumerable<string> strs;
            if (res.Content.Headers.TryGetValues(name, out strs))
            {
                action(strs.First());
                return true;
            }
            return false;
        }

        /// <summary>
        /// Invoked when a new range is downloaded.
        /// </summary>
        public event EventHandler<RangeDownloadedEventArgs> RangeDownloaded;

        static DateTime parseDateTime(string dateTime)
        {
            return DateTime.ParseExact(dateTime,
                "ddd, dd MMM yyyy HH:mm:ss 'UTC'",
                CultureInfo.InvariantCulture.DateTimeFormat,
                DateTimeStyles.AssumeUniversal);
        }
    }

    /// <summary>
    /// Used by <see cref="HttpStream.RangeDownloaded"/> event.
    /// </summary>
    public class RangeDownloadedEventArgs : EventArgs
    {
        /// <summary>
        /// Range newly downloaded.
        /// </summary>
        public Range NewlyLoaded { get; set; }
    }
}

