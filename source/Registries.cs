using System;
using System.Collections.Generic;
using System.Linq;

namespace UQLScribe.Registries;

public interface IRegistry
{
    BepInEx.BaseUnityPlugin plugin {get; }
    IEnumerable<UQLTag.Compound> Save();
    void Load(UQLTag.Wrapper? tag, RainWorldGame? game);
}

public interface IObserver
{
    BepInEx.BaseUnityPlugin plugin {get; }
    string GUID {get; }
    void Load(UQLTag.Wrapper? tag, RainWorldGame game);
}

public static class Registrar
{
    private static readonly Dictionary<string, IRegistry> registries = new();
    private static readonly HashSet<IObserver> observers = new();

    private static List<UQLTag.Wrapper> unclaimedData = new();

    public static void Register(IObserver observer)
    {
        observers.Add(observer);
    }

    public static void Register(IRegistry registry)
    {
        string guid = registry.plugin.Info.Metadata.GUID;
        if (registries.ContainsKey(guid))
        {
            throw new InvalidOperationException(
                $"Multiple registries attempted to register GUID '{guid}'"
            );
        }

        UQLTag.Wrapper? data = Pop(
            unclaimedData,
            x => x.label.val == guid
        );

        if (data != null) {
            UQLScribe.LInfo($"Loading unclaimed data to registry for GUID '{guid}'");
            registry.Load(data, null);
        }
        
        registries.Add(guid, registry);
    }

    internal static IEnumerable<UQLTag.Wrapper> OnSave()
    {
        return registries
        .Select(x => new UQLTag.Wrapper(
                new UQLTag.Label(x.Value.plugin.Info.Metadata.GUID),
                x.Value.Save())).Concat(unclaimedData);
    }
    public static UQLTag.Wrapper? RequestLoad(SlugcatStats.Name saveNum, string GUID)
    {
        if (Hooks.RequestLoad(saveNum) is not {} saveData)
            return null;
        UQLScribe.LInfo($"Loading requested save for GUID '{GUID}' and slugcat {saveNum}");
        return saveData.FirstOrDefault(x => x.label.val == GUID);
    }
    internal static void OnLoad(IEnumerable<UQLTag.Wrapper> saveData, RainWorldGame game)
    {
        List<UQLTag.Wrapper> remainingData = saveData.ToList();
        Dictionary<string, UQLTag.Wrapper> observedData = new();

        UQLScribe.LInfo("OnLoading!");

        foreach (var registryPair in registries)
        {
            IRegistry registry = registryPair.Value;
            string GUID = registryPair.Key;

            UQLTag.Wrapper? data = Pop(
                remainingData,
                x => x.label.val == GUID
                );

            if (data != null)
            {
                if (observedData.ContainsKey(GUID))
                    throw new InvalidOperationException(
                        $"Multiple save entries found for registry GUID '{GUID}'"
                    );
                observedData.Add(GUID, data);
            }
            
            UQLScribe.LInfo($"Loading data to registry for GUID '{GUID}'");
            registry.Load(data, game);
        }

        foreach (UQLTag.Wrapper wrapper in remainingData)
        {
            UQLScribe.LWarn(
                $"Save data under GUID '{wrapper.label.val}' was claimed by no registry"
            );
        }

        unclaimedData = remainingData;

        foreach (IObserver observer in observers)
        {
            UQLTag.Wrapper? data;

            if (!observedData.TryGetValue(observer.GUID, out data))
            {
                data = Pop(
                    remainingData,
                    x => x.label.val == observer.GUID
                );

                if (data != null)
                {
                    observedData.Add(observer.GUID, data);
                }
            }

            UQLScribe.LInfo($"Loading data to observer owned by '{observer.plugin.Info.Metadata.GUID}' for GUID '{observer.GUID}'");
            observer.Load(data, game);
        }
    }

    private static T? Pop<T>(this List<T> list, Predicate<T> match)
    {
        int index = list.FindIndex(match);

        if (index == -1)
            return default;

        T item = list[index];
        list.RemoveAt(index);
        return item;
    }
}