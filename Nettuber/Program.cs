using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using VTS.Core;
using WatsonWebserver;
using WatsonWebserver.Lite;
using WatsonWebserver.Core;
using Nettuber;
using WebSocketSharp;

// This is a simple example of how to use the VTS plugin in C#.
// You can use this as a starting point for your own plugin implementation

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args); // Create a host builder so the program doesn't exit immediately

ConsoleVTSLoggerImpl logger = new(); // Create a logger to log messages to the console (you can use your own logger implementation here like in the Advanced example)

CoreVTSPlugin plugin = new(logger, 60, "Puppeteer", "KariChary", "");

VTSEventHandler eventHandler = new(logger, plugin);

MeshLocator meshLocator = new(logger, eventHandler, plugin);

LoopManager loopManager;


// List<Action<VTSItemEventData>> itemConsumers = [];

string[] requestedMeshes = ["Forehead", "RightHand", "LeftHand"];

try
{
    await plugin.InitializeAsync(
        new WebSocketImpl(logger),
        new NewtonsoftJsonUtilityImpl(),
        new TokenStorageImpl(""),
        () => logger.LogWarning("Disconnected!"));
    logger.Log("Connected!");



    //await plugin.RequestPermission(VTSPermission.LoadCustomImagesAsItems);

    await meshLocator.PostAuthenticationCallbacks();

    loopManager = new LoopManager(meshLocator);
}

catch (VTSException error)
{
    logger.LogError(error); // Log any errors that occur during initialization
}

WebserverFactory factory = new WebserverFactory(9098, meshLocator);

factory.GetServer().StartAsync();

var host = builder.Build(); // Build the host

await host.RunAsync();
