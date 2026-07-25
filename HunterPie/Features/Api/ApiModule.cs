using HunterPie.DI;
using HunterPie.DI.Module;
using HunterPie.Features.Api.Services;

namespace HunterPie.Features.Api;

internal class ApiModule : IDependencyModule
{
    public void Register(IDependencyRegistry registry)
    {
        registry
            .WithSingle<ApiServerService>();
    }
}
