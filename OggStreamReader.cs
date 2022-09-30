// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.IO;
using NVorbis;
using NVorbis.Contracts;

namespace DiscordAudioTests;

public class OggStreamReader : IDisposable
{
    private const double GranuleSampleRate = 48000.0; // Granule position is always expressed in units of 48000hz
    private readonly Stream _stream;
    private readonly NVorbis.Contracts.IContainerReader _containerReader;

    private NVorbis.Contracts.IPacketProvider _packetProvider;
    private bool _endOfStream;

    public OggStreamReader(Stream stream)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _containerReader = new NVorbis.Ogg.ContainerReader(stream, true)
        {
            NewStreamCallback = ProccessNewStream
        };

        if (!_containerReader.TryInit())
        {
            throw new InvalidOperationException();
        }
    }

    private bool ProccessNewStream(NVorbis.Contracts.IPacketProvider packetProvider)
    {
        _packetProvider = packetProvider;

        if (CanSeek)
        {
            GranuleCount = _packetProvider.GetGranuleCount();
        }

        return true;
    }

    /// <summary>
    /// Gets a value indicating whether the current stream supports seeking.
    /// </summary>
    public bool CanSeek => _stream.CanSeek;

    /// Returns true if there is still another data packet to be decoded from the current Ogg stream.
    /// Note that this decoder currently only assumes that the input has 1 elementary stream with no splices
    /// or other fancy things.
    /// </summary>
    public bool HasNextPacket => !_endOfStream;

    /// <summary>
    /// Gets the position of the last granule in the page the packet is in.
    /// </summary>
    public long GranulePosition { get; private set; }

    /// <summary>
    /// Gets the current time in the stream.
    /// </summary>
    public TimeSpan CurrentTime => TimeSpan.FromSeconds(GranulePosition / GranuleSampleRate);

    /// <summary>
    /// Gets the total number of granules in this stream.
    /// </summary>
    public long GranuleCount { get; private set; }

    /// <summary>
    /// Gets the total time from the stream. Only available if the stream is seekable.
    /// </summary>
    public TimeSpan TotalTime => TimeSpan.FromSeconds(GranuleCount / GranuleSampleRate);

    /// <summary>
    /// Looks for the next opus data packet in the Ogg stream and queues it up.
    /// If the end of stream has been reached, this does nothing.
    /// </summary>
    public byte[] GetNextPacket()
    {
        if (_endOfStream)
        {
            return null;
        }

        var packet = _packetProvider.GetNextPacket();
        if (packet == null)
        {
            _endOfStream = true;
            return null;
        }

        if (packet.GranulePosition.HasValue)
        {
            GranulePosition = packet.GranulePosition.Value;
        }

        var buf = new byte[packet.BitsRemaining / 8];
        _ = packet.Read(buf, 0, packet.BitsRemaining / 8);
        packet.Done();

        return buf;
    }

    public int ReadNextPacket(Span<byte> buffer)
    {
        if (_endOfStream)
        {
            return 0;
        }

        var packet = _packetProvider.GetNextPacket();
        if (packet == null)
        {
            _endOfStream = true;
            return 0;
        }

        if (packet.GranulePosition.HasValue)
        {
            GranulePosition = packet.GranulePosition.Value;
        }

        var bufferLength = packet.BitsRemaining / 8;

        var buf = ArrayPool<byte>.Shared.Rent(bufferLength);

        try
        {
            var bytesRead = packet.Read(buf, 0, bufferLength);

            packet.Done();

            buf.CopyTo(buffer);

            return bytesRead;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buf, true);
        }
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

        var granulePosition = (long)(playbackTime.TotalSeconds * GranuleSampleRate);
        SeekToGranulePosition(granulePosition);
    }

    /// <summary>
    /// Seeks the stream for a valid packet at the specified granule position.
    /// </summary>
    /// <param name="granulePosition">The granule position.</param>
    public void SeekToGranulePosition(long granulePosition)
    {
        if (!CanSeek)
        {
            throw new InvalidOperationException("Stream is not seekable.");
        }

        if (granulePosition < 0 || granulePosition > GranuleCount)
        {
            throw new ArgumentOutOfRangeException(nameof(granulePosition));
        }

        _ = _packetProvider.SeekTo(granulePosition, 1, GetPacketGranules);

        GranulePosition = granulePosition;
    }

    private int GetPacketGranules(IPacket curPacket, bool isFirst)
    {
        // if it's a resync, there's not any audio data to return
        if (curPacket.IsResync)
        {
            return 0;
        }

        // if it's not an audio packet, there's no audio data (seems obvious, though...)
        return curPacket.ReadBit() ? 0 : 1;
    }

    public void Dispose()
    {
        _containerReader.Dispose();
        GC.SuppressFinalize(this);
    }
}
