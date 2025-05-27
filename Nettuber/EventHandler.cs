namespace Nettuber;

using System.Collections.Concurrent;
using System.Text.Json;
using VTS.Core;
using System.Runtime.Serialization;
using System.Net.Http.Headers;
using WatsonWebserver;

public class VTSEventHandler(IVTSLogger logger, CoreVTSPlugin plugin)
{
    private readonly CoreVTSPlugin plugin = plugin;

    private readonly Dictionary<Type, object> callbackDicts = [];

    private readonly IVTSLogger logger = logger;

    private ConcurrentDictionary<string, Action<K>> Callbacks<K>()
    {
        Action<K> kay = (_) => { };
        Type ktype = kay.GetType();
        if (callbackDicts.ContainsKey(ktype) && callbackDicts[ktype].GetType() == typeof(ConcurrentDictionary<string, Action<K>>))
        {
            return (ConcurrentDictionary<string, Action<K>>)callbackDicts[ktype];
        }
        ConcurrentDictionary<string, Action<K>> ret = new();
        callbackDicts[kay.GetType()] = ret;
        return ret;
    }

    public void RemoveEventCallback<K>(string id) {
        Callbacks<K>().TryRemove(id, out _);
    }

    public string AddEventCallback(Action<VTSTestEventData> onEvent)
    {
        string uuid = Guid.NewGuid().ToString();
        var callbacks = Callbacks<VTSTestEventData>();
        if (callbacks.IsEmpty)
            plugin.SubscribeToTestEvent(new(), e => activateCallbacks(callbacks, e), (_) => { }, (_) => { });
        callbacks[uuid] = onEvent;
        return uuid;
    }
    public string AddEventCallback(Action<VTSModelLoadedEventData> onEvent)
    {
        string uuid = Guid.NewGuid().ToString();
        var callbacks = Callbacks<VTSModelLoadedEventData>();
        if (callbacks.IsEmpty)
            plugin.SubscribeToModelLoadedEvent(new(), e => activateCallbacks(callbacks, e), (_) => { }, (_) => { });
        callbacks[uuid] = onEvent;
        return uuid;
    }  

    public string AddEventCallback(Action<VTSTrackingEventData> onEvent)
    {
        string uuid = Guid.NewGuid().ToString();
        var callbacks = Callbacks<VTSTrackingEventData>();
        if (callbacks.IsEmpty)
            plugin.SubscribeToTrackingEvent(e => activateCallbacks(callbacks, e), (_) => { }, (_) => { });
        callbacks[uuid] = onEvent;
        return uuid;
    }

    public string AddEventCallback(Action<VTSBackgroundChangedEventData> onEvent)
    {
        string uuid = Guid.NewGuid().ToString();
        var callbacks = Callbacks<VTSBackgroundChangedEventData>();
        if (callbacks.IsEmpty)
            plugin.SubscribeToBackgroundChangedEvent(e => activateCallbacks(callbacks, e), (_) => { }, (_) => { });
        callbacks[uuid] = onEvent;
        return uuid;
    }  

    public string AddEventCallback(Action<VTSModelConfigChangedEventData> onEvent)
    {
        string uuid = Guid.NewGuid().ToString();
        var callbacks = Callbacks<VTSModelConfigChangedEventData>();
        if (callbacks.IsEmpty)
            plugin.SubscribeToModelConfigChangedEvent(e => activateCallbacks(callbacks, e), (_) => { }, (_) => { });
        callbacks[uuid] = onEvent;
        return uuid;
    }  

    public string AddEventCallback(Action<VTSModelMovedEventData> onEvent)
    {
        string uuid = Guid.NewGuid().ToString();
        var callbacks = Callbacks<VTSModelMovedEventData>();
        if (callbacks.IsEmpty)
            plugin.SubscribeToModelMovedEvent(e => activateCallbacks(callbacks, e), (_) => { }, (_) => { });
        callbacks[uuid] = onEvent;
        return uuid;
    }  

    public string AddEventCallback(Action<VTSModelOutlineEventData> onEvent)
    {
        string uuid = Guid.NewGuid().ToString();
        var callbacks = Callbacks<VTSModelOutlineEventData>();
        if (callbacks.IsEmpty)
            plugin.SubscribeToModelOutlineEvent(new(), e => activateCallbacks(callbacks, e), (_) => { }, (_) => { });
        callbacks[uuid] = onEvent;
        return uuid;
    }

    public string AddEventCallback(Action<VTSHotkeyTriggeredEventData> onEvent)
    {
        string uuid = Guid.NewGuid().ToString();
        var callbacks = Callbacks<VTSHotkeyTriggeredEventData>();
        if (callbacks.IsEmpty)
            plugin.SubscribeToHotkeyTriggeredEvent(new(), e => activateCallbacks(callbacks, e), (_) => { }, (_) => { });
        callbacks[uuid] = onEvent;
        return uuid;
    }

    public string AddEventCallback(Action<VTSModelAnimationEventData> onEvent)
    {
        string uuid = Guid.NewGuid().ToString();
        var callbacks = Callbacks<VTSModelAnimationEventData>();
        if (callbacks.IsEmpty)
            plugin.SubscribeToModelAnimationEvent(new(), e => activateCallbacks(callbacks, e), (_) => { }, (_) => { });
        callbacks[uuid] = onEvent;
        return uuid;
    }

    public string AddEventCallback(Action<VTSItemEventData> onEvent)
    {
        string uuid = Guid.NewGuid().ToString();
        var callbacks = Callbacks<VTSItemEventData>();
        if (callbacks.IsEmpty)
            plugin.SubscribeToItemEvent(new(), e => activateCallbacks(callbacks, e), (_) => { }, (_) => { });
        callbacks[uuid] = onEvent;
        return uuid;
    }

    public string AddEventCallback(Action<VTSModelClickedEventData> onEvent)
    {
        string uuid = Guid.NewGuid().ToString();
        var callbacks = Callbacks<VTSModelClickedEventData>();
        if (callbacks.IsEmpty)
            plugin.SubscribeToModelClickedEvent(new(), e => activateCallbacks(callbacks, e), (_) => { }, (_) => { });
        callbacks[uuid] = onEvent;
        return uuid;
    }

    public string AddEventCallback(Action<VTSPostProcessingEventData> onEvent)
    {
        string uuid = Guid.NewGuid().ToString();
        var callbacks = Callbacks<VTSPostProcessingEventData>();
        if (callbacks.IsEmpty)
            plugin.SubscribeToPostProcessingEvent(new(), e => activateCallbacks(callbacks, e), (_) => { }, (_) => { });
        callbacks[uuid] = onEvent;
        return uuid;
    }  

    private void activateCallbacks<K>(ConcurrentDictionary<string, Action<K>> dict, K eventData) {
        Action<K> multidelegate = (_) => { };
        foreach (Action<K> a in dict.Values)
        {
            multidelegate += a;
        }
        multidelegate(eventData);
    }
}
