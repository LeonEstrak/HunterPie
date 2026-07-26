using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using HunterPie.Core.Observability.Logging;

namespace HunterPie.Features.Api.Server;

/// <summary>
/// Serves static files from a root directory (the WebUI bundle) with path
/// traversal protection, basic content types and cache headers.
/// </summary>
internal class StaticFileServer
{
    private static readonly IReadOnlyDictionary<string, string> ContentTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".html"] = "text/html; charset=utf-8",
            [".js"] = "text/javascript; charset=utf-8",
            [".mjs"] = "text/javascript; charset=utf-8",
            [".css"] = "text/css; charset=utf-8",
            [".json"] = "application/json; charset=utf-8",
            [".svg"] = "image/svg+xml",
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".webp"] = "image/webp",
            [".ico"] = "image/x-icon",
            [".woff"] = "font/woff",
            [".woff2"] = "font/woff2",
            [".ttf"] = "font/ttf",
            [".map"] = "application/json; charset=utf-8"
        };

    private readonly ILogger _logger = LoggerFactory.Create();
    private readonly string _rootFullPath;

    private StaticFileServer(string rootFullPath)
    {
        _rootFullPath = rootFullPath;
    }

    /// <summary>
    /// Creates a server for the given directory, or null when it does not
    /// exist (API-only mode).
    /// </summary>
    public static StaticFileServer? Create(string rootDirectory)
    {
        if (!Directory.Exists(rootDirectory))
            return null;

        string fullPath = Path.GetFullPath(rootDirectory);

        if (!File.Exists(Path.Combine(fullPath, "index.html")))
            return null;

        return new StaticFileServer(fullPath);
    }

    /// <summary>
    /// Attempts to serve the request path as a static file. Returns false
    /// when the file does not exist (caller should respond 404).
    /// </summary>
    public async Task<bool> TryServeAsync(string requestPath, NetworkStream stream, CancellationToken cancellationToken)
    {
        string relative = requestPath.TrimStart('/');

        if (relative.Length == 0)
            relative = "index.html";

        string fullPath = Path.GetFullPath(Path.Combine(_rootFullPath, relative));

        // Path traversal guard: the resolved path must stay inside the root
        if (!fullPath.StartsWith(_rootFullPath, StringComparison.OrdinalIgnoreCase))
            return false;

        if (Directory.Exists(fullPath))
            fullPath = Path.Combine(fullPath, "index.html");

        if (!File.Exists(fullPath))
            return false;

        string extension = Path.GetExtension(fullPath);
        string contentType = ContentTypes.TryGetValue(extension, out string? known)
            ? known
            : "application/octet-stream";

        // index.html must never be cached (it references hashed assets);
        // Vite's hashed /assets files can be cached aggressively.
        string cacheControl = fullPath.StartsWith(Path.Combine(_rootFullPath, "assets"), StringComparison.OrdinalIgnoreCase)
            ? "public, max-age=31536000, immutable"
            : "no-cache";

        byte[] content = await File.ReadAllBytesAsync(fullPath, cancellationToken);

        await Http.HttpResponse.WriteFileAsync(stream, contentType, cacheControl, content, cancellationToken);
        return true;
    }
}
