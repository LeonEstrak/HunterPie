using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HunterPie.Core.Observability.Logging;

namespace HunterPie.Features.Api.Server.WebSocket;

/// <summary>
/// Tracks connected WebSocket clients and fans out broadcast messages.
/// Dead connections are pruned on send failure.
/// </summary>
internal class WebSocketSessionManager
{
    private readonly ILogger _logger = LoggerFactory.Create();
    private readonly List<WebSocketConnection> _connections = new();
    private readonly Lock _lock = new();

    public int ClientCount
    {
        get { lock (_lock) return _connections.Count; }
    }

    public void Add(WebSocketConnection connection)
    {
        lock (_lock)
            _connections.Add(connection);

        _logger.Debug($"WebSocket client connected ({ClientCount} total)");
    }

    public void Remove(WebSocketConnection connection)
    {
        lock (_lock)
            _ = _connections.Remove(connection);

        _logger.Debug($"WebSocket client disconnected ({ClientCount} total)");
    }

    public async Task BroadcastAsync(string json)
    {
        WebSocketConnection[] snapshot;

        lock (_lock)
            snapshot = _connections.ToArray();

        foreach (WebSocketConnection connection in snapshot)
        {
            try
            {
                await connection.SendTextAsync(json);
            }
            catch (Exception err) when (err is IOException or ObjectDisposedException or OperationCanceledException)
            {
                Remove(connection);
            }
        }
    }
}
