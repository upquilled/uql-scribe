# Scribe: a Rain World utility mod

---

### *Unified persistent save data for YOUR MOD!*

To run the development version of this mod (this repository), please install [Extra Parameters](https://steamcommunity.com/sharedfiles/filedetails/?id=3557598109), as it is a required dependency. Then you may clone the repository into your `StreamingAssets/mods` directory.

Steam Workshop Item: []()

# Documentation

## Registering your save data

Inside the `UQLScribe` namespace, the module responsible for loading and saving persistent data is `UQLScribe.Registries`.
The data tree format used for saves - UQLTag - is specified in the section below.

There also several ways to access/store save data with this module:

### 1. IRegistry

`IRegistry` is the interface through which mods claim GUIDs in the save system. IRegistry has to implement the following properties and methods:

```cs
public interface IRegistry
{
    BepInEx.BaseUnityPlugin plugin {get; } // A reference to the IRegistry's owner mod

    void Load(UQLTag.Wrapper? tag, RainWorldGame? game); // This gets executed when a RainWorldGame is created,
                                                         // typically at the start of a new cycle;
                                                         // RainWorldGame? game is only null if this gets called
                                                         // due to the IRegistry being registered late with
                                                         // old data 

    IEnumerable<UQLTag.Compound> Save(); // This gets executed when the game is saved,
                                         // usually at the end of a successful cycle.
                                         // The returned sequence of Compounds will
                                         // be inserted into the saved Wrapper.
}
```
To register an `IRegistry`, run the method `Registrar.Register(myRegistry)` once your mod initializes.

### 2. IObserver

`IObserver` is the interface through which mods observe other mods' GUIDs in the save system. IObserver has to implement the following properties and methods:

```cs
public interface IRegistry
{
    BepInEx.BaseUnityPlugin plugin {get; } // A reference to the IObserver's owner mod

    string GUID {get; } // The GUID of the observed mod

    void Load(UQLTag.Wrapper? tag, RainWorldGame game); // This gets executed a RainWorldGame is created,
                                                        // typically at the start of a new cycle
}
```
To register an `IObserver`, run the method `Registrar.Register(myObserver)` once your mod initializes.

### 3. RequestLoad()

The method `UQLTag.Wrapper? Registrar.RequestLoad(SlugcatStats.Name saveNum, string GUID)` returns the save data of the mod of the specified GUID if it's present. This would usually be used if you want to load campaign data outside of the campaign itself (e.g. in the menu)

## The UQLTag data format 

The UQLTag format is the data format used to store saves in Scribe. The node types in UQLTag follow this tree:

```
Element
   |
   |--- Wrapper (Label, Compound[])
   |
   |--- Tag
         |--- Label : IRecordEntry (string)
         |
         |--- Compound
                 |
                 |--- Record (IRecordEntry[])
                 |
                 |--- Group : IRecordEntry (Compound[])
                 |
                 |--- NamedGroup : IRecordEntry (Label, Compound[])
```


