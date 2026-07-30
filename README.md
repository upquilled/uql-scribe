# Scribe: a Rain World utility mod

---

### *Unified persistent save data for YOUR MOD!*

To run the development version of this mod (this repository), please install [Extra Parameters](https://steamcommunity.com/sharedfiles/filedetails/?id=3557598109), as it is a required dependency. Then you may clone the repository into your `StreamingAssets/mods` directory.

Steam Workshop Item: []()

# Documentation

## Registering your save data

Inside the `UQLScribe` namespace, the module responsible for loading and saving persistent data is `UQLScribe.Registries`.
The data tree format used for saves — UQLTag — is specified in the section below.

There are several ways to access/store save data with this module:

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
                                                         // old save data present

    IEnumerable<UQLTag.Compound> Save(); // This gets executed when the game is saved,
                                         // usually at the end of a successful cycle.
                                         // The returned sequence of Compounds will
                                         // be inserted into the saved Wrapper as your mod's data.
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

The UQLTag format is the data format used to store saves in Scribe. The node types in UQLTag follow this inheritance tree:

```
Element
   │
   ├── Wrapper (Label, Compound[])
   │
   └── Tag
        ├── Label : IRecordEntry (string)
        │
        └── Compound
               │
               ├── Record (IRecordEntry[])
               │
               ├── Group : IRecordEntry (Compound[])
               │
               └── NamedGroup : IRecordEntry (Label, Compound[])
```

### Label(string val)
The Label is the sole leaf element type of a UQLTag tree, housing an arbitrary string. When serialized, it's displayed as the string literal with reserved characters (`<>:,\`) escaped. The string can contain any unicode symbols, including whitespace and newlines.
### Record(IEnumerable<IRecordEntry> entries)
A Record houses a collection of Labels, Groups and NamedGroups delimited by colons (`A:B:C`) when serialized. If a Record has only one entry, it must be a non-empty Label.
### Group(IEnumerable<Compound> compounds)
A Group houses a collection of Compounds — Records, Groups and NamedGroups. In serialization, the Group itself is delimited with angle brackets, and elements inside are delimited with commas if needed. 

For a Group like `<<innergroup>coolName<innergroup>A:B:C,<innergroupagain>,:A>`:
- There is no comma after the first Group and NamedGroup because `>` unambiguously closes it.
- The Record has a comma after it to clarify the following is not a continuation of the last entry.
- For the last Group, the comma between it and a Record with a leading empty Label serves to distinguish it from `<innergroupagain>:A`, which would be a single Record with the Group as its first entry.
### NamedGroup(Label label, IEnumerable<Compound> compounds)
A NamedGroup is a Group equipped with a Label. It's serialized exactly like a Group, except with the Label appended before the opening bracket. The Label of a NamedGroup cannot be empty.
### Wrapper(Label label, IEnumerable<Compound> compounds)
The Wrapper is the root element of a UQLTag tree, and hence the element which houses a mod's data. The Label of the Wrapper corresponds to the mod's GUID, and the Compounds inside correspond to the mod's save data. A Wrapper is serialized as `<label:compounds>`, where the Compounds inside are delimited the same way as in a Group.
### static Wrapper Parse(string input)
This is the user-facing method allowing you to parse a serialized wrapper back into a UQLTag tree.
