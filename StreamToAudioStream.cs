// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NextAudio;

namespace DiscordAudioTests;

public class StreamToAudioStream : ReadOnlyAudioStream
{
    private readonly Stream _sourceStream;

    public StreamToAudioStream(Stream sourceStream)
    {
        _sourceStream = sourceStream;
    }

    public override bool CanSeek => _sourceStream.CanSeek;

    public override long Length => _sourceStream.Length;

    public override long Position
    {
        get => _sourceStream.Position;
        set => _sourceStream.Position = value;
    }

    public override AudioStream Clone()
    {
        return new StreamToAudioStream(_sourceStream);
    }

    public override int Read(Span<byte> buffer)
    {
        var bytesReaded = _sourceStream.Read(buffer);

        return bytesReaded;
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        return _sourceStream.ReadAsync(buffer, cancellationToken);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        return _sourceStream.Seek(offset, origin);
    }

    protected override void Dispose(bool disposing)
    {
        if (IsDisposed)
        {
            return;
        }

        if (disposing)
        {
            _sourceStream.Dispose();
        }
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        if (IsDisposed)
        {
            return;
        }

        await _sourceStream.DisposeAsync();
    }
}
