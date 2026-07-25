using System;
using System.Threading;
using System.Threading.Tasks;
using HunterPie.Core.Client.Configuration.Versions;
using HunterPie.Core.Observability.Logging;
using HunterPie.Features.Api.Server;
using HunterPie.Features.Api.Server.WebSocket;
using HunterPie.Features.Api.Session;

namespace HunterPie.Features.Api.Services;

/// <summary>
/// Broadcast loop: flushes discrete events immediately and pushes dirty
/// snapshot sections to all WebSocket clients at the configured interval
/// (read from the config every iteration, so changes apply live).
/// </summary>
internal class ApiBroadcastService : IDisposable
{
    private readonly ILogger _logger = LoggerFactory.Create();
    private readonly GameSessionSnapshot _snapshot;
    private readonly WebSocketSessionManager _sessions;
    private readonly V5Config _config;

    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _loop;

    public ApiBroadcastService(
        GameSessionSnapshot snapshot,
        WebSocketSessionManager sessions,
        V5Config config)
    {
        _snapshot = snapshot;
        _sessions = sessions;
        _config = config;
    }

    public void Start()
    {
        _cancellationTokenSource = new CancellationTokenSource();
        _loop = Task.Run(() => RunAsync(_cancellationTokenSource.Token));
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            int interval = Math.Clamp((int)_config.Api.BroadcastInterval.Current, 50, 1000);

            try
            {
                await Task.Delay(interval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                // Flush discrete events even with no clients (keeps the queue bounded)
                object[] events = _snapshot.ConsumePendingEvents();

                if (_sessions.ClientCount == 0)
                {
                    _ = _snapshot.ConsumeDirtySections();
                    continue;
                }

                foreach (object evt in events)
                    await _sessions.BroadcastAsync(ApiJson.Serialize(evt));

                string[] dirtySections = _snapshot.ConsumeDirtySections();

                if (dirtySections.Length == 0)
                    continue;

                string stateJson = _snapshot.ExecuteLocked(snapshot =>
                    ApiStateSerializer.SerializeStateUpdate(snapshot, dirtySections));

                await _sessions.BroadcastAsync(stateJson);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception err)
            {
                _logger.Error($"API broadcast failed: {err}");
            }
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();

        try
        {
            _loop?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException) { }

        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
    }
}
