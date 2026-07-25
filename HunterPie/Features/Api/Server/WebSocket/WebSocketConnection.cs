using System;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HunterPie.Features.Api.Server.Http;

namespace HunterPie.Features.Api.Server.WebSocket;

/// <summary>
/// A single WebSocket client connection. Handles the RFC 6455 handshake,
/// the frame read loop (ping/pong/close, fragmented text messages) and
/// serialized writes.
/// </summary>
internal class WebSocketConnection
{
    private const string WEB_SOCKET_GUID = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private NetworkStream? _stream;

    /// <summary>
    /// Performs the upgrade handshake. Writes the error response itself and
    /// returns false when the request is not a valid WebSocket upgrade.
    /// </summary>
    public async Task<bool> TryHandshakeAsync(HttpRequest request, NetworkStream stream, CancellationToken cancellationToken)
    {
        string? key = request.GetHeader("Sec-WebSocket-Key");
        string? upgrade = request.GetHeader("Upgrade");

        if (key is null || !string.Equals(upgrade, "websocket", StringComparison.OrdinalIgnoreCase))
        {
            await HttpResponse.WriteErrorAsync(stream, 400, "invalid_websocket_upgrade", cancellationToken);
            return false;
        }

        string acceptKey = Convert.ToBase64String(
            SHA1.HashData(Encoding.ASCII.GetBytes(key + WEB_SOCKET_GUID))
        );

        string response =
            "HTTP/1.1 101 Switching Protocols\r\n" +
            "Upgrade: websocket\r\n" +
            "Connection: Upgrade\r\n" +
            $"Sec-WebSocket-Accept: {acceptKey}\r\n" +
            "\r\n";

        await stream.WriteAsync(Encoding.ASCII.GetBytes(response), cancellationToken);
        _stream = stream;
        return true;
    }

    public async Task SendTextAsync(string text, CancellationToken cancellationToken = default)
    {
        if (_stream is null)
            return;

        byte[] payload = Encoding.UTF8.GetBytes(text);

        await _sendLock.WaitAsync(cancellationToken);

        try
        {
            await WebSocketFrame.WriteAsync(_stream, WebSocketOpcode.Text, payload, cancellationToken);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>
    /// Frame read loop. Responds to protocol pings, applies close handshake
    /// and handles application-level {"type":"ping"} messages. Returns when
    /// the connection closes or fails.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (_stream is null)
            return;

        using var messageBuffer = new MemoryStream();

        while (!cancellationToken.IsCancellationRequested)
        {
            WebSocketFrame.Frame? frame = await WebSocketFrame.ReadAsync(_stream, cancellationToken);

            if (frame is null)
                return;

            switch (frame.Opcode)
            {
                case WebSocketOpcode.Ping:
                    await WriteControlAsync(WebSocketOpcode.Pong, frame.Payload, cancellationToken);
                    break;

                case WebSocketOpcode.Close:
                    await WriteControlAsync(WebSocketOpcode.Close, [], cancellationToken);
                    return;

                case WebSocketOpcode.Text:
                case WebSocketOpcode.Continuation:
                    messageBuffer.Write(frame.Payload, 0, frame.Payload.Length);

                    if (messageBuffer.Length > WebSocketFrame.MAX_MESSAGE_BYTES)
                        return;

                    if (frame.Fin)
                    {
                        string message = Encoding.UTF8.GetString(messageBuffer.ToArray());
                        messageBuffer.SetLength(0);
                        await HandleMessageAsync(message, cancellationToken);
                    }
                    break;

                case WebSocketOpcode.Binary:
                    // API clients are not expected to send binary data
                    break;
            }
        }
    }

    private async Task HandleMessageAsync(string message, CancellationToken cancellationToken)
    {
        if (message.Contains("\"ping\""))
            await SendTextAsync("{\"type\":\"pong\"}", cancellationToken);
    }

    private async Task WriteControlAsync(WebSocketOpcode opcode, byte[] payload, CancellationToken cancellationToken)
    {
        if (_stream is null)
            return;

        await _sendLock.WaitAsync(cancellationToken);

        try
        {
            await WebSocketFrame.WriteAsync(_stream, opcode, payload, cancellationToken);
        }
        finally
        {
            _sendLock.Release();
        }
    }
}
