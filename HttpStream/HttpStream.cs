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
	public class HttpStream : CacheStream
	{
		Uri _uri;
		HttpClient _httpClient;
		bool _ownHttpClient;
		public long Size { get; private set; }
		public bool SizeAvailable { get; private set; }
		public bool InspectionFinished { get; private set; }
		public DateTime LastModified { get; private set; }
		public int BufferingSize { get; set; }

		public HttpStream(Uri uri) : this(uri, new MemoryStream(), true)
		{
		}

		public HttpStream(Uri uri, Stream cache, bool ownStream) : this(uri, cache, ownStream, new HttpClient())
		{
		}

		public HttpStream(Uri uri, Stream cache, bool ownStream, HttpClient httpClient) : base(cache, ownStream)
		{
			BufferingSize = 1024 * 1024;
			Size = long.MaxValue;
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

		public override long GetStreamLengthOrFail(long defValue)
		{
			return SizeAvailable ? Size : defValue;
		}

		public override async Task<Range> LoadAsync(Stream stream, long pos, int count, CancellationToken cancellationToken)
		{
			if(count == 0)
				return new Range(pos, 0);

			// confirm block unit access
			long lowfilter = BufferingSize - 1;
			long begPos = pos & ~lowfilter;
			long endPos = (pos + count + lowfilter) & ~lowfilter;
			pos &= ~lowfilter;
			if(SizeAvailable && endPos > Size)
				endPos = Size;
			count = (int)(endPos - begPos);

			var req = new HttpRequestMessage(HttpMethod.Get, _uri);
			// Use "Range" header to sepcify the data offset and size
			req.Headers.Add("Range", string.Format("bytes={0}-{1}", begPos, begPos + count - 1));

			// post the request
			var res = await _httpClient.SendAsync(req, cancellationToken);
			Debug.WriteLine(string.Format("HTTP Status: {0} for bytes={1}-{2}", res.StatusCode, begPos, begPos + count - 1));

			if(!res.IsSuccessStatusCode)
				throw new Exception(string.Format("HTTP Status: {0} for bytes={1}-{2}", res.StatusCode, begPos, begPos + count - 1));

			// retrieve the resulting Content-Range
			bool getRanges = true;
			long begin = 0, end = long.MaxValue;
			long size = long.MaxValue;
			try
			{
				// 206
                var range = res.Content.Headers.GetValues("Content-Range").First();
				Debug.WriteLine(range);
				var m = Regex.Match(range, @"bytes\s+([0-9]+)-([0-9]+)/(\w+)");
				begin = long.Parse(m.Groups[1].Value);
				end = long.Parse(m.Groups[2].Value);
				size = end - begin + 1;

				if(!SizeAvailable)
				{
					var sz = m.Groups[3].Value;
					if (sz != "*")
					{
						Size = long.Parse(sz);
						SizeAvailable = true;
					}
				}

                Debug.WriteLine(string.Format("Req: {0}-{1} -> Res: {2} (of {3})", begin, end, size, Size));
			}
			catch
			{
				try
				{
					// In some case, there's no Content-Range but Content-Length
					// instead.
					getRanges = false;
					begin = 0;
                    Size = end = size = long.Parse(res.Content.Headers.GetValues("Content-Length").First());
                    SizeAvailable = true;
                    Debug.WriteLine(string.Format("Req: {0}-{1} -> Res: {2} (of {3})", begin, end, size, Size));
				}
				catch
				{
					Debug.WriteLine("No Content-Length...");
				}
			}

			try
			{
				LastModified = parseLastModified(res);
			}
			catch
			{
			}
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

			if(!SizeAvailable && !getRanges)
			{
				Size = copied;
				SizeAvailable = true;
			}

			return new Range(begin, copied);
		}

		DateTime parseLastModified(HttpResponseMessage res)
		{
            return parseDateTime(res.Content.Headers.GetValues("Last-Modified").First());
		}

		DateTime parseDateTime(string dateTime)
		{
			return DateTime.ParseExact(dateTime,
				"ddd, dd MMM yyyy HH:mm:ss 'UTC'",
				CultureInfo.InvariantCulture.DateTimeFormat,
				DateTimeStyles.AssumeUniversal);
		}
	}
}

