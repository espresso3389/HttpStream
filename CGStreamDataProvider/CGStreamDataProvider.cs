using CoreGraphics;
using ObjCRuntime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;
using System.Threading.Tasks;

namespace HttpStream
{
    /// <summary>
    /// Creates <see cref="CGDataProvider"/> from <see cref="Stream"/>.
    /// </summary>
    public class CGStreamDataProvider
    {
        private CGStreamDataProvider(Stream stream, bool ownStream, int bufferingSize)
        {
            _stream = stream;
            _ownStream = ownStream;
            _bufferingSize = bufferingSize;
        }

        Stream _stream;
        bool _ownStream;
        int _bufferingSize;

        /// <summary>
        /// Creates <see cref="CGDataProvider"/> from <see cref="Stream"/>.
        /// </summary>
        /// <param name="stream">Original data stream.</param>
        /// <param name="ownStream"><c>true</c> to instruct <see cref="CGDataProvider"/> object to close the stream on its cleanup.</param>
        /// <param name="getStreamSizeAsync">A function to get the stream size asynchronously. If it is <c>null</c>, the stream size will be obtained synchronously.</param>
        /// <param name="bufferingSize">Buffering size.</param>
        /// <returns></returns>
        public static async Task<CGDataProvider> CreateWithStreamAsync(Stream stream, bool ownStream, Func<Task<long>> getStreamSizeAsync, int bufferingSize = 1024 * 32)
        {
            if (stream == null)
                throw new ArgumentNullException("stream must not be null.", "stream");
            if (!stream.CanSeek || !stream.CanRead)
                throw new ArgumentException("stream is not seekable/readable.", "stream");

            var callbacks = new CGDataProviderDirectCallbacks
            {
                getBytesAtPosition = getBytesAtPosition,
                releaseInfo = releaseInfo
            };

			long streamLength = 0;
			if (getStreamSizeAsync != null)
			{
				streamLength = await getStreamSizeAsync();
			}
			else
			{
				var cs = stream as HttpStream.CacheStream;
				streamLength = cs == null ? (nint)stream.Length : await cs.GetLengthAsync().ConfigureAwait(false);
			}

            var gcHandle = GCHandle.Alloc(new CGStreamDataProvider(stream, ownStream, bufferingSize), GCHandleType.Normal);
            var dp = CGDataProviderCreateDirect(GCHandle.ToIntPtr(gcHandle), (nint)streamLength, callbacks);
            if (dp == IntPtr.Zero)
            {
                gcHandle.Free();
                throw new InvalidOperationException("CGStreamDataProvider.CreateWithStream failed due to CGDataProviderCreateDirect error.");
            }
            return new CoreGraphics.CGDataProvider(dp);
        }

        [MonoPInvokeCallback(typeof(CGDataProviderReleaseInfoCallback))]
        static void releaseInfo(IntPtr info)
        {
            try
            {
                //Debug.WriteLine(string.Format("CGStreamDataProvider.release({0})", info));
                var gcHandle = GCHandle.FromIntPtr(info);
                var pthis = (CGStreamDataProvider)gcHandle.Target;

                if (pthis._ownStream)
                    pthis._stream.Dispose();

                gcHandle.Free();
            }
            catch (Exception e)
            {
                Debug.WriteLine(string.Format("CGStreamDataProvider.release({0}): {1}", info, e));
            }
        }

        [MonoPInvokeCallback(typeof(CGDataProviderGetBytesAtPositionCallback))]
        static nint getBytesAtPosition(IntPtr info, IntPtr buffer, nint position, nint count)
        {
            try
            {
                //Debug.WriteLine(string.Format("CGStreamDataProvider.getBytesAtPosition(info={0},pos={1},count={2},end={3})", info, position, count, position + count));
                var pthis = (CGStreamDataProvider)GCHandle.FromIntPtr(info).Target;
                pthis._stream.Position = position;

                var buf = new byte[pthis._bufferingSize];
                nint bytesRead = 0;
                nint bytesToRead = count;
                while (bytesToRead > 0)
                {
                    var ret = pthis._stream.Read(buf, 0, (int)Math.Min(bytesToRead, buf.Length));
                    if (ret == 0)
                        break;

                    Marshal.Copy(buf, 0, buffer, ret);
                    buffer += ret;
                    bytesRead += ret;
                    bytesToRead -= ret;
                }
                return bytesRead;
            }
            catch (Exception e)
            {
                Debug.WriteLine(string.Format("CGStreamDataProvider.getBytesAtPosition(info={0},pos={1},count={2},end={3}): {4}", info, position, count, position + count, e));
                return 0;
            }
        }

        [DllImport(Constants.CoreGraphicsLibrary)]
        static extern IntPtr CGDataProviderCreateDirect(IntPtr info, nint size, CGDataProviderDirectCallbacks callbacks);

        [StructLayout(LayoutKind.Sequential)]
        struct CGDataProviderDirectCallbacks
        {
            public int version;
            public IntPtr getBytePointer;
            public IntPtr releaseBytePointer;
            public CGDataProviderGetBytesAtPositionCallback getBytesAtPosition;
            public CGDataProviderReleaseInfoCallback releaseInfo;
        };

        delegate nint CGDataProviderGetBytesAtPositionCallback(IntPtr info, IntPtr buffer, nint position, nint count);
        delegate void CGDataProviderReleaseInfoCallback(IntPtr info);
    }
}
