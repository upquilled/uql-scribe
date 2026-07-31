using System;
using System.Collections.Generic;
using System.Linq;

namespace UQLScribe.Registries;
using static UQLTag;

public interface IRegistry
{
    BepInEx.BaseUnityPlugin plugin {get; }
    IEnumerable<Compound>? Save();
    void Load(Wrapper? tag, RainWorldGame? game);
}

public interface IObserver
{
    BepInEx.BaseUnityPlugin plugin {get; }
    string GUID {get; }
    void Load(Wrapper? tag, RainWorldGame game);
}

public static class Registrar
{
    private static readonly Dictionary<string, IRegistry> registries = new();
    private static readonly HashSet<IObserver> observers = new();

    private static List<Wrapper> unclaimedData = new();

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

        Wrapper? data = Pop(
            unclaimedData,
            x => x.label.val == guid
        );

        if (data is not null) {
            UQLScribe.LInfo($"Loading unclaimed data to registry for GUID '{guid}'");
            var game = UQLScribe.rainWorldInstance
                       .processManager.currentMainLoop as RainWorldGame;
            
            if (game is null)
                UQLScribe.LWarn("Failed to capture game instance "
                              + "while loading unclaimed data!");
            registry.Load(data, game);
        }
        
        registries.Add(guid, registry);
    }

    internal static IEnumerable<Wrapper> OnSave()
    {
        return RegistryData().Concat(unclaimedData);
    }

    private static IEnumerable<Wrapper> RegistryData()
    {
        foreach (var reg in registries.Values)
        {
            string GUID = reg.plugin.Info.Metadata.GUID;
            UQLScribe.LInfo($"Saving entry for '{GUID}'");
            var result = reg.Save();
            if (result is null) continue;
            yield return new Wrapper(new Label(GUID),result);
        }
    }
    public static Wrapper? RequestLoad(SlugcatStats.Name saveNum, string GUID, out bool saveExists)
    {
        Wrapper? wrapper = Hooks.RequestLoadSpecific(saveNum, GUID, out saveExists);
        if (!saveExists) return null;

        UQLScribe.LInfo($"Loading requested save for GUID '{GUID}' "
                      + $"and slugcat {saveNum}");
        
        return wrapper;
    }
    internal static void OnLoad(IEnumerable<Wrapper> saveData, RainWorldGame game)
    {
        List<Wrapper> remainingData = saveData.ToList();
        Dictionary<string, Wrapper> observedData = new();

        UQLScribe.LInfo("OnLoading!");

        foreach (var registryPair in registries)
        {
            IRegistry registry = registryPair.Value;
            string GUID = registryPair.Key;

            Wrapper? data = Pop(
                remainingData,
                x => x.label.val == GUID
                );

            if (data is not null)
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

        foreach (Wrapper wrapper in remainingData)
        {
            UQLScribe.LWarn(
                $"Save data under GUID '{wrapper.label.val}' was "
                +"claimed by no registry"
            );
        }

        unclaimedData = remainingData;

        foreach (IObserver observer in observers)
        {
            Wrapper? data;

            if (!observedData.TryGetValue(observer.GUID, out data))
            {
                data = Pop(
                    remainingData,
                    x => x.label.val == observer.GUID
                );

                if (data is not null)
                {
                    observedData.Add(observer.GUID, data);
                }
            }

            UQLScribe.LInfo("Loading data to observer owned by "
                         + $"'{observer.plugin.Info.Metadata.GUID}' for "
                         + $"GUID '{observer.GUID}'");

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