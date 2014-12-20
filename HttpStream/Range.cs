using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;

namespace HttpStream
{
    /// <summary>
    /// A class to describe a range.
    /// </summary>
    public class Range : IComparable<Range>
    {
        /// <summary>
        /// Offset of the range.
        /// </summary>
        public long Offset;
        /// <summary>
        /// Length of the range.
        /// </summary>
        public long Length;

        /// <summary>
        /// Creates a new <see cref="Range"/> object.
        /// </summary>
        /// <param name="offset">Offset of the range.</param>
        /// <param name="length">Length of the range.</param>
        public Range(long offset, long length)
        {
            Offset = offset;
            Length = length;
        }

        /// <summary>
        /// The end of the range.
        /// </summary>
        public long End { get { return Offset + Length; } }

        /// <summary>
        /// Whether the specified range is completely inside this range.
        /// </summary>
        /// <param name="r">Range to check.</param>
        /// <returns><c>true</c> if the range is inside this range.</returns>
        public bool Contains(Range r)
        {
            if (r.Offset < Offset || r.End > End)
                return false;
            return true;
        }

        /// <summary>
        /// Whether the specified range is completely inside this range.
        /// </summary>
        /// <param name="offset">Offset.</param>
        /// <param name="length">Length.</param>
        /// <returns><c>true</c> if the range is inside this range.</returns>
        public bool Contains(long offset, long length)
        {
            if (offset < Offset || offset + length > End)
                return false;
            return true;
        }

        /// <summary>
        /// Whether the specified offset is inside this range.
        /// </summary>
        /// <param name="offset">Offset.</param>
        /// <returns><c>true</c> if the offset is inside this range.</returns>
        public bool Contains(long offset)
        {
            if (offset < Offset || offset >= End)
                return false;
            return true;
        }

        #region IComparable implementation

        /// <summary>
        /// Comparing two ranges according to their offsets.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public int CompareTo(Range other)
        {
            return (int)(Offset - other.Offset);
        }

        #endregion
    }
}

