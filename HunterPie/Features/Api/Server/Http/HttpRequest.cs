using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HunterPie.Features.Api.Server.Http;

/// <summary>
/// Minimal HTTP/1.1 request representation. Only the request line and
/// headers are parsed; bodies are not supported (the API is GET-only).
/// </summary>
internal class HttpRequest
{
    private const int MAX_HEADER_BYTES = 16 * 1024;
    private static readonly byte[] HeaderTerminator = [(byte)'\r', (byte)'\n', (byte)'\r', (byte)'\n'];

    public required string Method { get; init; }
    public required string Path { get; init; }
    public IReadOnlyDictionary<string, string> Headers { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Values captured by parameterized route templates (e.g. {index}).</summary>
    public IReadOnlyDictionary<string, string> RouteValues { get; set; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public string? GetHeader(string name) =>
        Headers.TryGetValue(name, out string? value) ? value : null;

    /// <summary>
    /// Reads a single HTTP request from the stream. Returns null when the
    /// connection is closed before any data arrives or the request is
    /// malformed beyond recovery.
    /// </summary>
    public static async Task<HttpRequest?> ReadAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        byte[] buffer = await ReadHeadersAsync(stream, cancellationToken);

        if (buffer.Length == 0)
            return null;

        string headerText = Encoding.ASCII.GetString(buffer);
        string[] lines = headerText.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length == 0)
            return null;

        string[] requestLine = lines[0].Split(' ');

        if (requestLine.Length < 2)
            return null;

        string path = requestLine[1];
        int queryIndex = path.IndexOf('?');

        if (queryIndex >= 0)
            path = path[..queryIndex];

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 1; i < lines.Length; i++)
        {
            int separator = lines[i].IndexOf(':');

            if (separator <= 0)
                continue;

            headers[lines[i][..separator].Trim()] = lines[i][(separator + 1)..].Trim();
        }

        return new HttpRequest
        {
            Method = requestLine[0].ToUpperInvariant(),
            Path = Uri.UnescapeDataString(path),
            Headers = headers
        };
    }

    private static async Task<byte[]> ReadHeadersAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        using var memory = new MemoryStream();
        var readBuffer = new byte[1024];

        while (memory.Length < MAX_HEADER_BYTES)
        {
            int read = await stream.ReadAsync(readBuffer, cancellationToken);

            if (read == 0)
                break;

            await memory.WriteAsync(readBuffer.AsMemory(0, read), cancellationToken);

            byte[] written = memory.ToArray();
            int end = written.AsSpan().IndexOf(HeaderTerminator);

            if (end >= 0)
                return written[..end];
        }

        return memory.ToArray();
    }
}
