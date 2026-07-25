using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using HunterPie.Features.Api.Server.Http;

namespace HunterPie.Features.Api.Server.Routing;

internal delegate Task ApiRouteHandler(HttpRequest request, NetworkStream stream, CancellationToken cancellationToken);

/// <summary>
/// Exact-segment router with support for single-segment parameters,
/// e.g. <c>/api/v1/monsters/{index}</c>.
/// </summary>
internal class RouteTable
{
    private record Route(string[] Segments, ApiRouteHandler Handler);

    private readonly List<Route> _routes = new();

    public void MapGet(string template, ApiRouteHandler handler)
    {
        string[] segments = template.Split('/', StringSplitOptions.RemoveEmptyEntries);
        _routes.Add(new Route(segments, handler));
    }

    public bool TryMatch(HttpRequest request, out ApiRouteHandler? handler)
    {
        handler = null;
        string[] requestSegments = request.Path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        foreach (Route route in _routes)
        {
            if (route.Segments.Length != requestSegments.Length)
                continue;

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            bool matches = true;

            for (int i = 0; i < route.Segments.Length; i++)
            {
                string templateSegment = route.Segments[i];

                if (templateSegment is ['{', .., '}'])
                {
                    values[templateSegment[1..^1]] = requestSegments[i];
                    continue;
                }

                if (!string.Equals(templateSegment, requestSegments[i], StringComparison.OrdinalIgnoreCase))
                {
                    matches = false;
                    break;
                }
            }

            if (!matches)
                continue;

            request.RouteValues = values;
            handler = route.Handler;
            return true;
        }

        return false;
    }

    public IEnumerable<string> Templates => _routes.Select(route => "/" + string.Join('/', route.Segments));
}
