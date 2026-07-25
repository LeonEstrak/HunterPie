using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using HunterPie.Core.Observability.Logging;
using HunterPie.Features.Api.Server.Http;
using HunterPie.Features.Api.Server.Routing;

namespace HunterPie.Features.Api.Server;

/// <summary>
/// Self-contained HTTP/1.1 server over <see cref="TcpListener"/>.
/// Deliberately not based on <see cref="HttpListener"/> (requires HTTP.sys
/// URL ACLs / admin rights) nor ASP.NET Core (would require users to install
/// the ASP.NET Core runtime).
/// </summary>
internal class ApiHttpServer : IDisposable
{
    private readonly ILogger _logger = LoggerFactory.Create();
    private readonly RouteTable _routes = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private TcpListener? _listener;
    private Task? _acceptLoop;

    public RouteTable Routes => _routes;

    public void Start(IPAddress address, int port)
    {
        _listener = new TcpListener(address, port);
        _listener.Start();

        _acceptLoop = Task.Run(AcceptLoopAsync);

        _logger.Info($"API server listening on {address}:{port}");
    }

    private async Task AcceptLoopAsync()
    {
        CancellationToken cancellationToken = _cancellationTokenSource.Token;

        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient? client = null;

            try
            {
                client = await _listener!.AcceptTcpClientAsync(cancellationToken);
            }
            catch (OperationCanceledException) { break; }
            catch (SocketException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (Exception err)
            {
                _logger.Error($"API server accept loop failed: {err}");
            }

            if (client is null)
                continue;

            _ = HandleConnectionAsync(client, cancellationToken);
        }
    }

    private async Task HandleConnectionAsync(TcpClient client, CancellationToken cancellationToken)
    {
        try
        {
            using (client)
            {
                NetworkStream stream = client.GetStream();
                HttpRequest? request = await HttpRequest.ReadAsync(stream, cancellationToken);

                if (request is null)
                    return;

                if (request.Method == "OPTIONS")
                {
                    await HttpResponse.WriteCorsPreflightAsync(stream, cancellationToken);
                    return;
                }

                if (request.Method != "GET")
                {
                    await HttpResponse.WriteErrorAsync(stream, 405, "method_not_allowed", cancellationToken);
                    return;
                }

                if (!_routes.TryMatch(request, out ApiRouteHandler? handler) || handler is null)
                {
                    await HttpResponse.WriteErrorAsync(stream, 404, "not_found", cancellationToken);
                    return;
                }

                await handler(request, stream, cancellationToken);
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
        catch (SocketException) { }
        catch (Exception err)
        {
            _logger.Error($"API server connection failed: {err}");
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _listener?.Stop();

        try
        {
            _acceptLoop?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException) { }

        _cancellationTokenSource.Dispose();
    }
}
