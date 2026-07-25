using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using HunterPie.Core.Client;
using HunterPie.Core.Client.Configuration.Versions;
using HunterPie.Core.Observability.Logging;
using HunterPie.Features.Api.Server;
using HunterPie.Features.Api.Server.Http;
using HunterPie.Features.Api.Server.WebSocket;

namespace HunterPie.Features.Api.Services;

/// <summary>
/// Owns the lifetime of the API HTTP/WebSocket server and registers all
/// routes. Started from <see cref="MainApplication"/> and disposed on exit.
/// </summary>
internal class ApiServerService : IDisposable
{
    public const int API_VERSION = 1;

    private readonly ILogger _logger = LoggerFactory.Create();
    private readonly V5Config _config;
    private readonly DateTime _startedAt = DateTime.UtcNow;

    private ApiHttpServer? _server;
    private readonly WebSocketSessionManager _sessions = new();

    public ApiServerService(V5Config config)
    {
        _config = config;
    }

    public bool IsRunning => _server is not null;

    public void Start()
    {
        if (!_config.Api.Enable)
        {
            _logger.Info("API server is disabled in the configuration");
            return;
        }

        IPAddress address = _config.Api.BindAllInterfaces
            ? IPAddress.Any
            : IPAddress.Loopback;

        int port = (int)_config.Api.Port.Current;

        try
        {
            _server = new ApiHttpServer();
            MapRoutes(_server);
            _server.Start(address, port);
        }
        catch (SocketException err)
        {
            _logger.Error($"Failed to start API server on {address}:{port}: {err.Message}");
            _server?.Dispose();
            _server = null;
        }
    }

    private void MapRoutes(ApiHttpServer server)
    {
        server.Routes.MapGet("/", Authenticated(HandleIndex));
        server.Routes.MapGet("/api/v1/status", Authenticated(HandleStatus));
        server.Routes.MapGet("/ws", Authenticated(HandleWebSocket));
    }

    /// <summary>
    /// Wraps a route with bearer token authentication when an auth token
    /// is configured. Empty token means open access.
    /// </summary>
    private Server.Routing.ApiRouteHandler Authenticated(Server.Routing.ApiRouteHandler handler)
    {
        return async (request, stream, cancellationToken) =>
        {
            string? expectedToken = _config.Api.AuthToken.Value;

            if (!string.IsNullOrEmpty(expectedToken))
            {
                string? authorization = request.GetHeader("Authorization");

                if (authorization != $"Bearer {expectedToken}")
                {
                    await HttpResponse.WriteErrorAsync(stream, 401, "unauthorized", cancellationToken);
                    return;
                }
            }

            await handler(request, stream, cancellationToken);
        };
    }

    private Task HandleIndex(HttpRequest request, NetworkStream stream, CancellationToken cancellationToken)
    {
        var index = new
        {
            name = "HunterPie API",
            version = API_VERSION,
            endpoints = _server!.Routes.Templates.ToArray()
        };

        return HttpResponse.WriteJsonAsync(stream, 200, ApiJson.Serialize(index), cancellationToken);
    }

    private Task HandleStatus(HttpRequest request, NetworkStream stream, CancellationToken cancellationToken)
    {
        var status = new
        {
            hunterPieVersion = ClientInfo.Version.ToString(),
            apiVersion = API_VERSION,
            uptimeSeconds = (long)(DateTime.UtcNow - _startedAt).TotalSeconds,
            game = (object?)null
        };

        return HttpResponse.WriteJsonAsync(stream, 200, ApiJson.Serialize(status), cancellationToken);
    }

    private async Task HandleWebSocket(HttpRequest request, NetworkStream stream, CancellationToken cancellationToken)
    {
        var connection = new WebSocketConnection();

        if (!await connection.TryHandshakeAsync(request, stream, cancellationToken))
            return;

        _sessions.Add(connection);

        var hello = new
        {
            type = "hello",
            data = new
            {
                apiVersion = API_VERSION,
                game = (object?)null
            }
        };

        await connection.SendTextAsync(ApiJson.Serialize(hello), cancellationToken);

        try
        {
            await connection.RunAsync(cancellationToken);
        }
        finally
        {
            _sessions.Remove(connection);
        }
    }

    public void Dispose()
    {
        _server?.Dispose();
        _server = null;
    }
}
