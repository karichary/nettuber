namespace Nettuber;

using System.Collections.Concurrent;
using VTS.Core;
using System.Text.Json;
using Microsoft.VisualBasic;
using System.ComponentModel;
using System.Runtime.Serialization;

public class MeshLocator
{
    ConcurrentDictionary<string, ArtMeshCoordinate> modelLoci = new();
    private string dataPath = "./modelLocs.json";

    public void LoadLocations()
    {
        string meshData = File.ReadAllText(dataPath);
        modelLoci = JsonSerializer.Deserialize<ConcurrentDictionary<string, ArtMeshCoordinate>>(meshData) ?? throw new SerializationException { };     
    }
    public void SaveLocations()
    {
        string jsonString = JsonSerializer.Serialize(modelLoci);
        File.WriteAllText(dataPath, jsonString);
    }

    public async void RegisterLocations(string[] locNames, bool specifyArtMesh)
    {
        foreach (var loc in locNames)
        {
             
        }
        throw new NotImplementedException { };
    }
}
