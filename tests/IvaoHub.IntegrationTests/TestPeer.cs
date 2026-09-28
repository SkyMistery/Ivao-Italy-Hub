using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// Puts the neighbour a test names under the request, before anything of the hub runs. The test server has no socket,
/// so the neighbour is said in a header, <see cref="Header"/>; the forwarded headers middleware then judges it exactly
/// as it judges the web server in front of Passenger.
/// </summary>
internal sealed class TestPeerStartupFilter : IStartupFilter
{
    public const string Header = "X-Test-Peer";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => builder =>
    {
        builder.Use(async (context, continuation) =>
        {
            if (IPAddress.TryParse(context.Request.Headers[Header].ToString(), out var peer))
            {
                context.Connection.RemoteIpAddress = peer;
                context.Connection.RemotePort = 50000;
            }

            await continuation();
        });

        next(builder);
    };
}
