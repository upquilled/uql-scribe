using System;
using System.Collections.Generic;
using System.Linq;

namespace UQLScribe.Registries;

public interface IObserver
{
    string GUID { get; }

    void Load(UQLTag.Wrapper? tag);
}
public interface IRegistry : IObserver
{
    UQLTag.Wrapper Save();
}

public static class Registrar
{
    private static readonly Dictionary<string, IRegistry> registries = new();
    private static readonly HashSet<IObserver> observers = new();

    public static void Register(IObserver observer)
    {
        if (observer is IRegistry registry)
        {
            if (registries.ContainsKey(registry.GUID))
            {
                throw new InvalidOperationException(
                    $"Multiple registries attempted to register GUID '{registry.GUID}'"
                );
            }

            registries.Add(registry.GUID, registry);
        }
        else
        {
            observers.Add(observer);
        }
    }   

    internal static IEnumerable<UQLTag.Wrapper> OnSave()
    {
        return registries
        .Select(x => x.Value.Save());
    }
    internal static void OnLoad(IEnumerable<UQLTag.Wrapper> saveData)
    {
        List<UQLTag.Wrapper> unclaimedData = saveData.ToList();
        Dictionary<string, UQLTag.Wrapper> observedData = new();

        foreach (var registryPair in registries) 
        {
            IRegistry registry = registryPair.Value;
            string GUID = registryPair.Key;

            UQLTag.Wrapper? data = Pop(
                unclaimedData,
                x => x.label.val == GUID
                );

            if (data != null)
            {
                observedData.Add(GUID, data);
            }

            registry.Load(data);
        }

        foreach (UQLTag.Wrapper wrapper in unclaimedData)
        {
            UQLScribe.LWarn(
                $"Save data under GUID '{wrapper.label.val}' was claimed by no registry"
            );
        }

        foreach (IObserver observer in observers)
        {
            UQLTag.Wrapper? data;

            if (!observedData.TryGetValue(observer.GUID, out data))
            {
                data = Pop(
                    unclaimedData,
                    x => x.label.val == observer.GUID
                );

                if (data != null)
                {
                    observedData.Add(observer.GUID, data);
                }
            }

            observer.Load(data);
        }
    }

    private static T? Pop<T> (this List<T> list, Predicate<T> match)
    {
        int index = list.FindIndex(match);

        if (index == -1)
            return default;

        T item = list[index];
        list.RemoveAt(index);
        return item;
    }
}