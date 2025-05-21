using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using VTS.Core;
using WatsonWebserver;
using WatsonWebserver.Lite;
using WatsonWebserver.Core;

// This is a simple example of how to use the VTS plugin in C#.
// You can use this as a starting point for your own plugin implementation

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args); // Create a host builder so the program doesn't exit immediately

ConsoleVTSLoggerImpl logger = new(); // Create a logger to log messages to the console (you can use your own logger implementation here like in the Advanced example)

CoreVTSPlugin plugin = new(logger, 60, "Puppeteer", "KariChary", "");

// List<Action<VTSItemEventData>> itemConsumers = [];

string[] requestedMeshes = ["Forehead", "RightHand", "LeftHand"];
 
// Mesh name identifier -> coordinate
ConcurrentDictionary<string, ArtMeshCoordinate> modelLoci = new();


try
{
    await plugin.InitializeAsync(
        new WebSocketImpl(logger),
        new NewtonsoftJsonUtilityImpl(),
        new TokenStorageImpl(""),
        () => logger.LogWarning("Disconnected!"));
    logger.Log("Connected!");

    //await plugin.RequestPermission(VTSPermission.LoadCustomImagesAsItems);

    var apiState = await plugin.GetAPIState();


    logger.Log("Using VTubeStudio " + apiState.data.vTubeStudioVersion);
    var currentModel = await plugin.GetCurrentModel();
    var clickSubscription = await plugin.SubscribeToModelClickedEvent(
        new VTSModelClickedEventConfigOptions(),
        async (clickData) =>
        {
            // logger.Log(JsonConvert.SerializeObject(clickData, Formatting.Indented));
            // Only right click on model registers
            if (!clickData.data.modelWasClicked || clickData.data.mouseButtonID != 1) { return; }
            ArtMeshCoordinate coord = new();
            foreach (ArtMeshHit mesh in clickData.data.artMeshHits)
            {
                if (mesh.artMeshOrder == 0)
                {
                    coord = mesh.hitInfo;
                    break;
                }
            }
            if (coord.modelID == "") { return; }

            var checkerItem = await plugin.LoadItem("transp.png", new VTSItemLoadOptions());
            string inst = checkerItem.data.instanceID;
            while (true)
            {
                try
                {
                    var pinResult = await plugin.PinItemToPoint(
                        inst,
                        coord.modelID,
                        coord.artMeshID,
                        0.0f,
                        VTSItemAngleRelativityMode.RelativeToModel,
                        0.01f,
                        VTSItemSizeRelativityMode.RelativeToWorld,
                        coord.ToBarycentricCoordinate());
                    if (pinResult == null) { break; }
                    Thread.Sleep(1000);
                }
                catch (VTSException error)
                {
                    if (error.ErrorData.data.errorID != ErrorID.ItemPinRequestGivenItemNotLoaded)
                    {
                        throw;
                    }
                    logger.Log("Ending tracking.");
                    break;
                }

            }
            // VTSItemUnloadOptions unloadInst = new() {
            //    itemInstanceIDs = [inst]
            //};
            // await plugin.UnloadItem(unloadInst);
        }
    );
    var itemSubscription = await plugin.SubscribeToItemEvent(
        new VTSItemEventConfigOptions(),
        (itemData) =>
        {
            logger.Log(JsonConvert.SerializeObject(itemData.data, Formatting.Indented));
            /*
            foreach (Action<VTSItemEventData> action in itemConsumers)
            {
                action(itemData);
            }
            */
        }
    );
    logger.Log("The current model is: " + currentModel.data.modelName);

    // Subscribe to your events here using the plugin.SubscribeTo* methods
    await plugin.SubscribeToBackgroundChangedEvent((backgroundInfo) =>
    {
        logger.Log("The background was changed to: " + backgroundInfo.data.backgroundName);
    });
    // To unsubscribe, use the plugin.UnsubscribeFrom* methods
}

catch (VTSException error)
{
    logger.LogError(error); // Log any errors that occur during initialization
}
WebserverSettings settings = new WebserverSettings("127.0.0.1", 9098);
WebserverBase server = new WatsonWebserver.Lite.WebserverLite(settings, DefaultRoute);

server.StartAsync();

var host = builder.Build(); // Build the host

await host.RunAsync();


static async Task DefaultRoute(HttpContextBase ctx) =>
  await ctx.Response.Send("Hello from the default route!");
