using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using HunterPie.Core.Client;
using HunterPie.Core.Client.Configuration.Versions;
using HunterPie.Core.Observability.Logging;
using HunterPie.Features.Api.Models;
using HunterPie.Features.Api.Server;
using HunterPie.Features.Api.Server.Http;
using HunterPie.Features.Api.Server.WebSocket;
using HunterPie.Features.Api.Session;

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
    private readonly WebSocketSessionManager _sessions;
    private readonly ApiBroadcastService _broadcastService;
    private readonly GameSessionSnapshot _snapshot;

    public ApiServerService(
        V5Config config,
        GameSessionSnapshot snapshot,
        WebSocketSessionManager sessions,
        ApiBroadcastService broadcastService)
    {
        _config = config;
        _snapshot = snapshot;
        _sessions = sessions;
        _broadcastService = broadcastService;
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
            _server = new ApiHttpServer
            {
                StaticFiles = StaticFileServer.Create(Path.Combine(ClientInfo.ClientPath, "WebUI"))
            };

            if (_server.StaticFiles is not null)
                _logger.Info("API server is serving the WebUI bundle");

            MapRoutes(_server);
            _server.Start(address, port);
            _broadcastService.Start();
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
        // When a WebUI bundle is present, "/" belongs to the static files
        // (they are consulted on route miss); the API index moves to /api/v1.
        if (server.StaticFiles is null)
            server.Routes.MapGet("/", Authenticated(HandleIndex));

        server.Routes.MapGet("/api/v1", Authenticated(HandleIndex));
        server.Routes.MapGet("/api/v1/status", Authenticated(HandleStatus));
        server.Routes.MapGet("/api/v1/game", Authenticated(WithSession((snapshot, _) => snapshot.Game)));
        server.Routes.MapGet("/api/v1/player", Authenticated(WithSession((snapshot, _) => snapshot.Player)));
        server.Routes.MapGet("/api/v1/party", Authenticated(WithSession((snapshot, _) => snapshot.Party)));
        server.Routes.MapGet("/api/v1/monsters", Authenticated(WithSession((snapshot, _) => snapshot.Monsters)));
        server.Routes.MapGet("/api/v1/monsters/{index}", Authenticated(HandleMonsterByIndex));
        server.Routes.MapGet("/api/v1/quest", Authenticated(WithSession((snapshot, _) => snapshot.Quest)));
        server.Routes.MapGet("/api/v1/chat", Authenticated(WithSession((snapshot, _) => snapshot.Chat)));
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

    /// <summary>
    /// Wraps a route that requires an active game session. Responds 503
    /// when no game is connected, 204 when the requested section is null
    /// (e.g. no active quest), otherwise serializes the section under the
    /// snapshot lock.
    /// </summary>
    private Server.Routing.ApiRouteHandler WithSession(Func<GameSessionSnapshot, HttpRequest, object?> sectionSelector)
    {
        return (request, stream, cancellationToken) =>
        {
            (bool hasSession, object? section) = _snapshot.ExecuteLocked(snapshot =>
                (snapshot.HasSession, sectionSelector(snapshot, request)));

            if (!hasSession)
                return HttpResponse.WriteErrorAsync(stream, 503, "no_active_session", cancellationToken);

            if (section is null)
                return HttpResponse.WriteJsonAsync(stream, 204, "null", cancellationToken);

            string json = _snapshot.ExecuteLocked(_ => ApiJson.Serialize(section));
            return HttpResponse.WriteJsonAsync(stream, 200, json, cancellationToken);
        };
    }

    private Task HandleMonsterByIndex(HttpRequest request, NetworkStream stream, CancellationToken cancellationToken)
    {
        if (!request.RouteValues.TryGetValue("index", out string? indexText) || !int.TryParse(indexText, out int index))
            return HttpResponse.WriteErrorAsync(stream, 400, "invalid_index", cancellationToken);

        (bool hasSession, MonsterDto? monster) = _snapshot.ExecuteLocked(snapshot =>
            (snapshot.HasSession, snapshot.Monsters.FirstOrDefault(it => it.Index == index)));

        if (!hasSession)
            return HttpResponse.WriteErrorAsync(stream, 503, "no_active_session", cancellationToken);

        if (monster is null)
            return HttpResponse.WriteErrorAsync(stream, 404, "monster_not_found", cancellationToken);

        string json = _snapshot.ExecuteLocked(_ => ApiJson.Serialize(monster));
        return HttpResponse.WriteJsonAsync(stream, 200, json, cancellationToken);
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
        object game = _snapshot.ExecuteLocked(snapshot => snapshot.HasSession
            ? (object)new
            {
                connected = true,
                type = snapshot.GameType,
                processName = snapshot.GameProcessName,
                processId = snapshot.GameProcessId
            }
            : new { connected = false });

        var status = new
        {
            hunterPieVersion = ClientInfo.Version.ToString(),
            apiVersion = API_VERSION,
            uptimeSeconds = (long)(DateTime.UtcNow - _startedAt).TotalSeconds,
            webSocketClients = _sessions.ClientCount,
            game
        };

        return HttpResponse.WriteJsonAsync(stream, 200, ApiJson.Serialize(status), cancellationToken);
    }

    private async Task HandleWebSocket(HttpRequest request, NetworkStream stream, CancellationToken cancellationToken)
    {
        var connection = new WebSocketConnection();

        if (!await connection.TryHandshakeAsync(request, stream, cancellationToken))
            return;

        _sessions.Add(connection);

        string hello = _snapshot.ExecuteLocked(snapshot => ApiJson.Serialize(new
        {
            type = "hello",
            data = new
            {
                apiVersion = API_VERSION,
                game = snapshot.GameType
            }
        }));

        await connection.SendTextAsync(hello, cancellationToken);

        string fullSnapshot = _snapshot.ExecuteLocked(ApiStateSerializer.SerializeFullSnapshot);
        await connection.SendTextAsync(fullSnapshot, cancellationToken);

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
        _broadcastService.Dispose();
        _server?.Dispose();
        _server = null;
    }
}
