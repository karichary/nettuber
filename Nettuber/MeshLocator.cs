
using System.Collections.Concurrent;
using VTS.Core;
using System.Text.Json;
using Microsoft.VisualBasic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Runtime.InteropServices;
using System.ComponentModel.Design;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Configuration.Assemblies;
using System.Collections.Immutable;
using Microsoft.Extensions.FileProviders;

namespace Nettuber;

public class MeshLocator
{
    Dictionary<string, Dictionary<string, ArtMeshCoordinate>> modelLoci = new();
    private readonly string dataPath = "./modelLocs.json";

    readonly VTSEventHandler handler;

    readonly CoreVTSPlugin plugin;

    private readonly IVTSLogger logger;

    public IJsonUtility JsonUtility { get; private set; }

    private readonly ConcurrentQueue<Tuple<string, bool>> toRegister = [];

    bool inSelectionDialogue = false;

    string callbackUuid = "";

    class UnregisteredLocationException : Exception;

    Dictionary<string, string> trackersToLocs = new();

    Dictionary<string, Tuple<float, float>> locations = new();

    public string CurrentModel { get; private set; }

    public MeshLocator(IVTSLogger logger, VTSEventHandler handler, CoreVTSPlugin plugin)
    {
        this.logger = logger;
        this.handler = handler;
        this.plugin = plugin;
        this.JsonUtility = new NewtonsoftJsonUtilityImpl();
        Initialize();
    }

    public void Initialize()
    {
        LoadLocations();
    }

    public void LoadLocations()
    {
        try
        {
            string meshData = File.ReadAllText(dataPath);
            modelLoci = JsonUtility.FromJson<Dictionary<string, Dictionary<string, ArtMeshCoordinate>>>(meshData) ?? throw new SerializationException { };
        }
        catch (FileNotFoundException)
        {
            return;
        }
    }
    public void SaveLocations()
    {
        string jsonString = JsonUtility.ToJson(modelLoci);
        logger.Log($"Saving {jsonString}");
        File.WriteAllText(dataPath, jsonString);
    }

    private string CreateTrackingCallback() {
        logger.Log("created tracking callback");
        return handler.AddEventCallback(
            async e =>
            {
                if (trackersToLocs.ContainsKey(e.data.itemInstanceID) && e.data.itemEventType == VTSItemEventType.DroppedPinned)
                {
                    locations[trackersToLocs[e.data.itemInstanceID]] = new Tuple<float, float>(e.data.itemPosition.x, e.data.itemPosition.y);
                }
            }
        );
    }

    public async Task PostAuthenticationCallbacks()
    {
        CreateTrackingCallback();
        callbackUuid = CreateRegCallback();
        await CreateCurrentModelCallback();

    }

    private async Task<string> CreateCurrentModelCallback() {
        if (CurrentModel == null)
        {
            var currentModelData = await plugin.GetCurrentModel();
            logger.Log($"Current model: {currentModelData.data.modelID}");
            CurrentModel = currentModelData.data.modelID;
        }
        return handler.AddEventCallback(
            async e =>
            {
                if (e.data.modelLoaded) this.CurrentModel = e.data.modelID;
            }
        );
    }

    private string CreateRegCallback()
    {
        return handler.AddEventCallback(
            async e =>
            {
                if (e.data.modelWasClicked && e.data.mouseButtonID == 1 && !inSelectionDialogue)
                {

                    if (!toRegister.TryPeek(out Tuple<string, bool> locInfo)) return;
                    Dictionary<string, ArtMeshHit> artMeshes = [];
                    foreach (var hit in e.data.artMeshHits)
                    {
                        artMeshes[hit.hitInfo.artMeshID] = hit;
                        if (!locInfo.Item2 && hit.artMeshOrder == 0)
                        {
                            if (!toRegister.TryDequeue(out Tuple<string, bool> newLoc)) return;
                            if (newLoc != locInfo) return;
                            if (!modelLoci.ContainsKey(locInfo.Item1))
                            {
                                modelLoci[locInfo.Item1] = new();
                            }
                            modelLoci[locInfo.Item1][hit.hitInfo.modelID] = hit.hitInfo;
                            SaveLocations();
                            return;
                        }
                    }
                    string jsonString = JsonUtility.ToJson(e.data);
                    logger.Log($"Artmeshes: {jsonString}");
                    inSelectionDialogue = true;
                    var selection = await plugin.RequestArtMeshSelection(
                        $"Choose the mesh associated with the location {locInfo.Item1} or cancel to retry.",
                        "",
                        1,
                        artMeshes.Keys);
                    inSelectionDialogue = false;
                    if (!selection.data.success) return;
                    if (selection.data.activeArtMeshes.Length != 1) return;
                    var meshId = selection.data.activeArtMeshes[0];
                    if (!artMeshes.ContainsKey(meshId)) return;
                    if (!toRegister.TryDequeue(out Tuple<string, bool> stillLoc)) return;
                    if (locInfo != stillLoc) return;
                    if (!modelLoci.ContainsKey(locInfo.Item1))
                    {
                        modelLoci[locInfo.Item1] = new();
                    }
                    modelLoci[locInfo.Item1][artMeshes[meshId].hitInfo.modelID] = artMeshes[meshId].hitInfo;
                    logger.Log("Added {locItem.Item1}");
                    SaveLocations();
                    return;
                }
            }
        );
    }

    private async Task<List<string>> getAllTrackingItemsInScene()
    {
        VTSItemListOptions opts = new();
        opts.onlyItemsWithFileName = "transp.png";
        opts.includeItemInstancesInScene = true;
        var currentTrackers = await plugin.GetItemList(opts);
        List<string> trackers = [];
        foreach (var inst in currentTrackers.data.itemInstancesInScene)
        {
            trackers.Add(inst.instanceID);
        }
        return trackers;
    }

    private List<string> getTrackableLocations(string[] trackedLocations, bool trackAll)
    {
        HashSet<string> trackables = [];
        foreach (var locus in modelLoci)
        {
            if (locus.Value.ContainsKey(CurrentModel))
            {
                logger.Log($"Can track {locus.Key}");
                trackables.Add(locus.Key);
            }
        }

        if (trackAll)
        {
            return trackables.ToList();
        }
        foreach (var loc in trackedLocations)
        {
            logger.Log($"want to track: {loc}");
        }
        trackables.IntersectWith(trackedLocations);
        return trackables.ToList();
    }

    public async void UpdateTrackingItems(string[] trackedLocations, bool trackAll)
    {
        var trackers = await getAllTrackingItemsInScene();
        var trackables = getTrackableLocations(trackedLocations, trackAll);

        int excess = trackers.Count() - trackables.Count();
        logger.Log($"{trackers.Count()} trackers found for {trackables.Count()} locs");
        if (excess > 0)
        {
            VTSItemUnloadOptions opts = new();
            opts.itemInstanceIDs = trackers.GetRange(0, excess).ToArray();
            await plugin.UnloadItem(opts);
            trackers.RemoveRange(0, excess);
        }
        if (excess < 0)
        {
            for (int i = 0; i < -excess; i++)
            {
                var newItem = await plugin.LoadItem("transp.png", new());
                trackers.Add(newItem.data.instanceID);
            }
        }
        trackersToLocs.Clear();
        for (int j = 0; j < trackers.Count(); j++) {
            trackersToLocs.Add(trackers[j], trackables[j]);
        }
        PinTrackers();
    }

    public void RegisterLocations(string[] locNames, bool specifyArtMesh)
    {
        foreach (string loc in locNames)
        {
            toRegister.Enqueue(new Tuple<string, bool>(loc, specifyArtMesh));
        }
        if (callbackUuid == "")
        {
            callbackUuid = CreateRegCallback();
        }
    }

    public async Task<Tuple<float, float>> GetPosition(string loc)
    {
        logger.Log($"Locations: {locations}, TrackerToLocs: {trackersToLocs}, currentModel: {CurrentModel}");
        if (!locations.ContainsKey(loc) || !trackersToLocs.Values.Contains(loc))
        {
            throw new UnregisteredLocationException();
        }
        return locations[loc];
    }

    public async void Tick()
    {
        PinTrackers();
    }

    private async void PinTrackers() {

        Parallel.ForEach(trackersToLocs,
        async (item) =>
        {
            var trackerId = item.Key;
            var loc = item.Value;
            ArtMeshCoordinate coord = modelLoci[loc][CurrentModel];
            await plugin.PinItemToPoint(
                trackerId,
                coord.modelID,
                coord.artMeshID,
                0.0f,
                VTSItemAngleRelativityMode.RelativeToModel,
                0.01f,
                VTSItemSizeRelativityMode.RelativeToWorld,
                coord.ToBarycentricCoordinate());
        });
    }
}
