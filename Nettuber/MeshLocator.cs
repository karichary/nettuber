
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

    bool inSelectionDialogue;

    string callbackUuid = "";

    int _tickInterval = 50;

    class UnregisteredLocationException : Exception;

    Dictionary<string, string> trackersToLocs;

	private readonly CancellationTokenSource _cancelToken;

    private Task _trackLoop;

    Dictionary<string, Tuple<float, float>> locations;

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
        this._trackLoop = TrackLoop(this._cancelToken.Token);
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

    public async void UpdateTrackingItems(string[] trackedLocations, bool trackAll) {
        VTSItemListOptions opts = new();
        opts.onlyItemsWithFileName = "transp.png";
        opts.includeItemInstancesInScene = true;
        var currentTrackers = await plugin.GetItemList(opts);
        var trackers = new HashSet<string>();
        foreach (var inst in currentTrackers.data.itemInstancesInScene)
        {
            trackers.Add(inst.instanceID);
        }
        string[] removable = [];
        foreach (var pair in trackersToLocs)
        {
            if (!trackers.Contains(pair.Key) || (!trackAll && !trackedLocations.Contains(pair.Value)))
            {
                removable.Append(pair.Key);
                continue;
            }
        }
        foreach (var remove in removable)
        {
            trackersToLocs.Remove(remove);
        }
        // unloadables
        trackers.ExceptWith(trackersToLocs.Keys);

        string[] missingLocations = [];
        foreach (var location in modelLoci.Keys)
        {
            if (!trackAll && !trackedLocations.Contains(location)) continue;
            if (!modelLoci[location].ContainsKey(CurrentModel)) continue;
            if (trackersToLocs.Values.Contains(location)) continue;
            missingLocations.Append(location);
        }
        if (missingLocations.Count() >= trackers.Count)
        {

            int excess = missingLocations.Count() - trackers.Count;
            for (int i = 0; i < excess; i++)
            {
                var newItem = await plugin.LoadItem("transp.png", new());
                trackersToLocs[newItem.data.instanceID] = missingLocations[i];
            }
            int j = excess;
            foreach (string tracker in trackers)
            {
                trackersToLocs[tracker] = missingLocations[j];
                j++;
            }
        }
        else
        {
            int excessTrack = trackers.Count - missingLocations.Count();
            int k = 0;
            foreach (string tracker in trackers)
            {
                trackersToLocs[tracker] = missingLocations[k];
                k++;
                if (k == excessTrack) break;
            }
            trackers.ExceptWith(trackersToLocs.Keys);
            var unloadOpts = new VTSItemUnloadOptions();
            unloadOpts.itemInstanceIDs = trackers.ToArray<string>();
            await plugin.UnloadItem(unloadOpts);
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
        if (!locations.ContainsKey(loc) || !trackersToLocs.Values.Contains(loc))
        {
            throw new UnregisteredLocationException();
        }
        return locations[loc];
    }

    private async Task TrackLoop(CancellationToken token)
    {
        float intervalInSeconds = ((float)this._tickInterval) / 1000f;
        while (!token.IsCancellationRequested)
        {
            PinTrackers();
            await Task.Delay(this._tickInterval);
        }
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
