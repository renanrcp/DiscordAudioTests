// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using NextAudio;
using YoutubeExplode.Videos.Streams;

namespace DiscordAudioTests;

public sealed class YoutubeAudioStream : ReadOnlyAudioStream
{
    private static readonly SocketError[] _errors = new SocketError[]
    {
        SocketError.ConnectionReset,
        SocketError.TimedOut,
        SocketError.NetworkReset,
    };

    private readonly HttpClient _httpClient;
    private readonly AudioOnlyStreamInfo _streamInfo;
    private readonly YoutubeStreamPolicy _policy;


    private Stream _sourceStream;
    private HttpResponseMessage _response;
    private long _position;

    public YoutubeAudioStream(HttpClient httpClient, AudioOnlyStreamInfo streamInfo)
    {
        _httpClient = httpClient;
        _streamInfo = streamInfo;
        _policy = YoutubeStreamPolicy.CreatePolicy();
    }

    public override bool CanSeek => _sourceStream.CanSeek;

    public override long Length => _sourceStream.Length;

    public override long Position
    {
        get => _sourceStream.Position;
        set => _sourceStream.Position = value;
    }

    public override YoutubeAudioStream Clone()
    {
        return new YoutubeAudioStream(_httpClient, _streamInfo);
    }

    public override int Read(Span<byte> buffer)
    {
        var bufferArray = ArrayPool<byte>.Shared.Rent(buffer.Length);

        try
        {
            var memory = bufferArray.AsMemory(0, buffer.Length);

            var readed = ReadAsync(memory).AsTask().GetAwaiter().GetResult();

            memory.Span.CopyTo(buffer);

            return readed;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bufferArray);
        }
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        return InternalReadAsync(buffer, true, cancellationToken);
    }

    private async ValueTask<int> InternalReadAsync(Memory<byte> buffer, bool attemptReconnect, CancellationToken cancellationToken)
    {
        await CheckConnectedAsync();

        try
        {
            var bytesReaded = await _sourceStream.ReadAsync(buffer, cancellationToken);

            _position += bytesReaded;

            return bytesReaded;
        }
        catch (Exception ex)
        {
            if (!attemptReconnect)
            {
                throw;
            }

            if (ex is not SocketException socketEx)
            {
                socketEx = ex.InnerException as SocketException;
            }

            if (socketEx == null)
            {
                throw;
            }

            if (!_errors.Contains(socketEx.SocketErrorCode))
            {
                throw;
            }

            await ClearAsync();

            return await InternalReadAsync(buffer, false, cancellationToken);
        }
    }

    private async ValueTask CheckConnectedAsync()
    {
        if (_sourceStream != null)
        {
            return;
        }

        _response = await MakeRequestAsync();

        if (_response.IsSuccessStatusCode)
        {
            var stream = await _response.Content.ReadAsStreamAsync();
            _sourceStream = new BufferedStream(stream);
        }
    }

    private Task<HttpResponseMessage> MakeRequestAsync()
    {
        return _policy.ExecuteAsync(() =>
        {
            var uri = _position > 0
                ? AddRangeParam(new Uri(_streamInfo.Url), _position, _streamInfo.Size.Bytes)
                : new Uri(_streamInfo.Url);

            using var request = new HttpRequestMessage(HttpMethod.Get, uri);

            return _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        });
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        _position = _sourceStream.Seek(offset, origin);
        return _position;
    }

    public static Uri AddRangeParam(Uri uri, long position, long length)
    {
        var httpValueCollection = HttpUtility.ParseQueryString(uri.Query);

        httpValueCollection.Remove("range");
        httpValueCollection.Add("range", $"{position}-{length}");

        var ub = new UriBuilder(uri)
        {
            Query = httpValueCollection.ToString()
        };

        return ub.Uri;
    }

    private async ValueTask ClearAsync()
    {
        if (_sourceStream != null)
        {
            await _sourceStream.DisposeAsync();
            _sourceStream = null;
        }

        if (_response != null)
        {
            _response.Dispose();
            _response = null;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (IsDisposed)
        {
            return;
        }

        if (disposing)
        {
            _sourceStream?.Dispose();
            _response?.Dispose();
        }
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        if (IsDisposed)
        {
            return;
        }

        if (_sourceStream != null)
        {
            await _sourceStream.DisposeAsync();
        }

        _response?.Dispose();
    }

    public static YoutubeAudioStream CreateStream(HttpClient httpClient, AudioOnlyStreamInfo streamInfo)
    {
        var stream = new YoutubeAudioStream(httpClient, streamInfo);

        return stream;
    }
}
