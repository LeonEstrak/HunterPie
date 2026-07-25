using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace HunterPie.Features.Api.Server.WebSocket;

internal enum WebSocketOpcode : byte
{
    Continuation = 0x0,
    Text = 0x1,
    Binary = 0x2,
    Close = 0x8,
    Ping = 0x9,
    Pong = 0xA
}

/// <summary>
/// RFC 6455 frame codec. Client-to-server frames are required to be masked
/// and are unmasked on read; server-to-client frames are written unmasked.
/// </summary>
internal static class WebSocketFrame
{
    public const int MAX_MESSAGE_BYTES = 64 * 1024;

    public record Frame(bool Fin, WebSocketOpcode Opcode, byte[] Payload);

    public static async Task<Frame?> ReadAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        byte[]? header = await ReadExactAsync(stream, 2, cancellationToken);

        if (header is null)
            return null;

        bool fin = (header[0] & 0b1000_0000) != 0;
        var opcode = (WebSocketOpcode)(header[0] & 0b0000_1111);
        bool masked = (header[1] & 0b1000_0000) != 0;
        long length = header[1] & 0b0111_1111;

        if (length == 126)
        {
            byte[]? extended = await ReadExactAsync(stream, 2, cancellationToken);

            if (extended is null)
                return null;

            length = (extended[0] << 8) | extended[1];
        }
        else if (length == 127)
        {
            byte[]? extended = await ReadExactAsync(stream, 8, cancellationToken);

            if (extended is null)
                return null;

            length = 0;

            for (int i = 0; i < 8; i++)
                length = (length << 8) | extended[i];
        }

        if (length > MAX_MESSAGE_BYTES)
            return null;

        byte[]? mask = null;

        if (masked)
        {
            mask = await ReadExactAsync(stream, 4, cancellationToken);

            if (mask is null)
                return null;
        }

        byte[]? payload = await ReadExactAsync(stream, (int)length, cancellationToken);

        if (payload is null)
            return null;

        if (mask is not null)
            for (int i = 0; i < payload.Length; i++)
                payload[i] ^= mask[i % 4];

        return new Frame(fin, opcode, payload);
    }

    public static async Task WriteAsync(
        NetworkStream stream,
        WebSocketOpcode opcode,
        byte[] payload,
        CancellationToken cancellationToken
    )
    {
        byte[] header = payload.Length switch
        {
            <= 125 => [(byte)(0x80 | (byte)opcode), (byte)payload.Length],
            <= ushort.MaxValue => [(byte)(0x80 | (byte)opcode), 126, (byte)(payload.Length >> 8), (byte)payload.Length],
            _ => BuildLongHeader(opcode, payload.Length)
        };

        await stream.WriteAsync(header, cancellationToken);
        await stream.WriteAsync(payload, cancellationToken);
    }

    private static byte[] BuildLongHeader(WebSocketOpcode opcode, int length)
    {
        var header = new byte[10];
        header[0] = (byte)(0x80 | (byte)opcode);
        header[1] = 127;

        for (int i = 0; i < 8; i++)
            header[2 + i] = (byte)((long)length >> ((7 - i) * 8));

        return header;
    }

    private static async Task<byte[]?> ReadExactAsync(NetworkStream stream, int count, CancellationToken cancellationToken)
    {
        var buffer = new byte[count];
        var offset = 0;

        while (offset < count)
        {
            int read;

            try
            {
                read = await stream.ReadAsync(buffer.AsMemory(offset, count - offset), cancellationToken);
            }
            catch (IOException)
            {
                return null;
            }

            if (read == 0)
                return null;

            offset += read;
        }

        return buffer;
    }
}
