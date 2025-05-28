using System.Collections.Concurrent;
using Newtonsoft.Json;

using WatsonWebserver;
using WatsonWebserver.Lite;
using WatsonWebserver.Core;
using WebSocketSharp;

namespace Nettuber
{
    public class WebserverFactory
    {
        readonly MeshLocator meshLocator;
        readonly int port;

        public WebserverFactory(int port, MeshLocator meshLocator) {
            this.port = port;
            this.meshLocator = meshLocator;
        }

        public WebserverBase GetServer() {

            WebserverSettings settings = new("127.0.0.1", port);
            WebserverBase server = new WebserverLite(settings, DefaultRoute);
            server.Routes.PreAuthentication.Parameter.Add(WatsonWebserver.Core.HttpMethod.GET, "/registerLocs/{locs}", RegisterLocationsRoute, ExceptionRoute);
            server.Routes.PreAuthentication.Parameter.Add(WatsonWebserver.Core.HttpMethod.GET, "/getLoc/{loc}", GetLocationRoute, ExceptionRoute);
            server.Routes.PreAuthentication.Parameter.Add(WatsonWebserver.Core.HttpMethod.GET, "/track/{locs}", StartTrackingRoute, ExceptionRoute);
            server.Routes.PreAuthentication.Parameter.Add(WatsonWebserver.Core.HttpMethod.GET, "/trackAll", StartTrackingAllRoute, ExceptionRoute);
            return server;
        }
    
        async Task RegisterLocationsRoute(HttpContextBase ctx)
        {
            if (!ctx.Request.Url.Parameters.Contains("locs"))
            {
                ctx.Response.StatusCode = 400;
            }
            else
            {
                meshLocator.RegisterLocations(ctx.Request.Url.Parameters["locs"].Split(','), true);
            }
            await ctx.Response.Send("");
        }


        async Task GetLocationRoute(HttpContextBase ctx)
        {
            string resp = "";
            if (!ctx.Request.Url.Parameters.Contains("loc"))
            {
                ctx.Response.StatusCode = 400;
            }
            else
            {
                var loc = await meshLocator.GetPosition(ctx.Request.Url.Parameters["loc"]);
                resp = $"{loc.Item1},{loc.Item2}";
            }
            await ctx.Response.Send(resp);
        }

        async Task StartTrackingRoute(HttpContextBase ctx)
        {
            if (!ctx.Request.Url.Parameters.Contains("locs"))
            {
                ctx.Response.StatusCode = 400;
            }
            else
            {
                meshLocator.UpdateTrackingItems(ctx.Request.Url.Parameters["locs"].Split(','), false);
            }
            await ctx.Response.Send("");
        }

        async Task StartTrackingAllRoute(HttpContextBase ctx) {
            meshLocator.UpdateTrackingItems([], true);
            await ctx.Response.Send("");
        }


        async Task DefaultRoute(HttpContextBase ctx) =>
        await ctx.Response.Send("Hello from the default route!");

        async Task ExceptionRoute(HttpContextBase cts, Exception e)
        {
            cts.Response.StatusCode = 500;
            await cts.Response.Send(e.Message);
        }

    }
}