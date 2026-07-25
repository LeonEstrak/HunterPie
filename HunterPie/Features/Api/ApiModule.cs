using HunterPie.DI;
using HunterPie.DI.Module;
using HunterPie.Features.Api.Server.WebSocket;
using HunterPie.Features.Api.Services;
using HunterPie.Features.Api.Session;

namespace HunterPie.Features.Api;

internal class ApiModule : IDependencyModule
{
    public void Register(IDependencyRegistry registry)
    {
        registry
            .WithSingle<GameSessionSnapshot>()
            .WithSingle<WebSocketSessionManager>()
            .WithSingle<ApiContextTracker>()
            .WithSingle<ApiBroadcastService>()
            .WithSingle<ApiServerService>();
    }
}
