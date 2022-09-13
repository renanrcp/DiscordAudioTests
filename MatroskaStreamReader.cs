using System;
using System.IO;

namespace DiscordAudioTests
{
    public class MatroskaStreamReader
    {
        private readonly Stream _stream;
        private bool _endOfStream;

        public MatroskaStreamReader(Stream stream)
        {
            _stream = stream;
        }

        /// <summary>
        /// Gets a value indicating whether the current stream supports seeking.
        /// </summary>
        public bool CanSeek => _stream.CanSeek;

        ///<summary>
        /// Returns true if there is still another data packet to be decoded from the current Ogg stream.
        /// Note that this decoder currently only assumes that the input has 1 elementary stream with no splices
        /// or other fancy things.
        /// </summary>
        public bool HasNextPacket => !_endOfStream;

        /// <summary>
        /// Gets the current time in the stream.
        /// </summary>
        public TimeSpan CurrentTime { get; }

        /// <summary>
        /// Gets the total time from the stream. Only available if the stream is seekable.
        /// </summary>
        public TimeSpan TotalTime { get; }

        /// <summary>
        /// Looks for the next data packet in the matroska stream and queues it up.
        /// If the end of stream has been reached, this does nothing.
        /// </summary>
        public byte[] GetNextPacket()
        {
            return null;
        }

        /// <summary>
        /// Seeks the stream for a valid packet at the specified playbackTime. Note that this is the best approximated position.
        /// </summary>
        /// <param name="playbackTime">The playback time.</param>
        public void SeekTo(TimeSpan playbackTime)
        {
            if (!CanSeek)
            {
                throw new InvalidOperationException("Stream is not seekable.");
            }

            if (playbackTime < TimeSpan.Zero || playbackTime > TotalTime)
            {
                throw new ArgumentOutOfRangeException(nameof(playbackTime));
            }
        }
    }

    public class MatroskaElement
    {
        public int Level { get; init; }

        public long Id { get; init; }

        public MatroskaElementType Type { get; init; }
        public long Position { get; init; }
        public int HeaderSize { get; init; }
        public int DataSiz { get; init; }
    }

    public enum MatroskaElementType
    {
    }
}