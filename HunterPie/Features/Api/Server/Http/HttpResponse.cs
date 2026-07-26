using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HunterPie.Features.Api.Server.Http;

/// <summary>
/// Helpers to write HTTP/1.1 responses. Every response is sent with
/// <c>Connection: close</c> (no keep-alive) and permissive CORS headers
/// so browser-hosted WebUIs can consume the API from any origin.
/// </summary>
internal static class HttpResponse
{
    private static readonly IReadOnlyDictionary<int, string> StatusTexts = new Dictionary<int, string>
    {
        [200] = "OK",
        [204] = "No Content",
        [400] = "Bad Request",
        [401] = "Unauthorized",
        [404] = "Not Found",
        [405] = "Method Not Allowed",
        [500] = "Internal Server Error",
        [503] = "Service Unavailable"
    };

    public static Task WriteJsonAsync(
        NetworkStream stream,
        int statusCode,
        string json,
        CancellationToken cancellationToken = default
    ) => WriteAsync(
        stream,
        statusCode,
        json,
        "application/json; charset=utf-8",
        cancellationToken
    );

    public static Task WriteErrorAsync(
        NetworkStream stream,
        int statusCode,
        string error,
        CancellationToken cancellationToken = default
    ) => WriteJsonAsync(
        stream,
        statusCode,
        $"{{\"error\":\"{error}\"}}",
        cancellationToken
    );

    public static async Task WriteFileAsync(
        NetworkStream stream,
        string contentType,
        string cacheControl,
        byte[] content,
        CancellationToken cancellationToken = default
    )
    {
        string headers =
            "HTTP/1.1 200 OK\r\n" +
            $"Content-Type: {contentType}\r\n" +
            $"Content-Length: {content.Length}\r\n" +
            $"Cache-Control: {cacheControl}\r\n" +
            "Access-Control-Allow-Origin: *\r\n" +
            "Connection: close\r\n" +
            "\r\n";

        await stream.WriteAsync(System.Text.Encoding.ASCII.GetBytes(headers), cancellationToken);
        await stream.WriteAsync(content, cancellationToken);
    }

    public static async Task WriteCorsPreflightAsync(NetworkStream stream, CancellationToken cancellationToken = default)
    {
        string headers =
            "HTTP/1.1 204 No Content\r\n" +
            "Access-Control-Allow-Origin: *\r\n" +
            "Access-Control-Allow-Methods: GET, OPTIONS\r\n" +
            "Access-Control-Allow-Headers: Authorization, Content-Type\r\n" +
            "Access-Control-Max-Age: 86400\r\n" +
            "Connection: close\r\n" +
            "\r\n";

        byte[] payload = Encoding.ASCII.GetBytes(headers);
        await stream.WriteAsync(payload, cancellationToken);
    }

    private static async Task WriteAsync(
        NetworkStream stream,
        int statusCode,
        string body,
        string contentType,
        CancellationToken cancellationToken
    )
    {
        byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
        string statusText = StatusTexts.TryGetValue(statusCode, out string? text) ? text : "Unknown";

        string headers =
            $"HTTP/1.1 {statusCode} {statusText}\r\n" +
            $"Content-Type: {contentType}\r\n" +
            $"Content-Length: {bodyBytes.Length}\r\n" +
            "Access-Control-Allow-Origin: *\r\n" +
            "Connection: close\r\n" +
            "\r\n";

        byte[] headerBytes = Encoding.ASCII.GetBytes(headers);

        await stream.WriteAsync(headerBytes, cancellationToken);
        await stream.WriteAsync(bodyBytes, cancellationToken);
    }
}
