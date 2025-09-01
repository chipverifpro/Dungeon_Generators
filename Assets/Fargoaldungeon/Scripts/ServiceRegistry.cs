using System;
using System.Collections.Generic;
using UnityEngine;

public interface IRegistrableService
{
    string ServiceKey { get; }  // e.g., "DungeonGenerator", "TimeManager"
}

public class ServiceRegistry : MonoBehaviour
{
    public static ServiceRegistry Instance { get; private set; }

    // single instance per key
    private readonly Dictionary<string, Component> _byKey = new();
    // by type (multiple allowed)
    private readonly Dictionary<Type, List<Component>> _byType = new();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject); // keep across scene loads
    }

    public void Register(Component c, string key)
    {
        if (!_byKey.ContainsKey(key)) _byKey[key] = c;

        var t = c.GetType();
        if (!_byType.TryGetValue(t, out var list)) _byType[t] = list = new List<Component>();
        if (!list.Contains(c)) list.Add(c);
    }

    public void Unregister(Component c, string key)
    {
        if (_byKey.TryGetValue(key, out var current) && current == c) _byKey.Remove(key);
        if (_byType.TryGetValue(c.GetType(), out var list)) list.Remove(c);
    }

    public T Get<T>() where T : Component
        => _byType.TryGetValue(typeof(T), out var list) && list.Count > 0 ? (T)list[0] : null;

    public T GetByKey<T>(string key) where T : Component
        => _byKey.TryGetValue(key, out var c) ? (T)c : null;

    public IReadOnlyList<T> GetAll<T>() where T : Component
    {
        if (_byType.TryGetValue(typeof(T), out var list))
            return list.ConvertAll(i => (T)i);
        return Array.Empty<T>();
    }
}

/* Example of usage:

public class DungeonGenerator : MonoBehaviour, IRegistrableService
{
    public string ServiceKey => "DungeonGenerator";

    void Awake()
    {
        // ensure there is a registry in the scene (drop one prefab in a bootstrap scene)
        ServiceRegistry.Instance.Register(this, ServiceKey);
    }

    void OnDestroy()
    {
        if (ServiceRegistry.Instance) ServiceRegistry.Instance.Unregister(this, ServiceKey);
    }
}

usage examples:

var gen = ServiceRegistry.Instance.Get<DungeonGenerator>();
// or by key:
var tm = ServiceRegistry.Instance.GetByKey<TimeManager>("TimeManager");


Tip: Put a ServiceRegistry GameObject in your bootstrap scene and mark it DontDestroyOnLoad.

*/