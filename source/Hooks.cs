using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Kittehface.Framework20;

namespace UQLScribe;
using static UQLTag;

public static class Hooks
{

    public static void ApplyInit()
    {
        UQLScribe.LoggerInstance.LogInfo("Applying hooks...");
        On.SaveState.SaveToString += SaveState_SaveToString;
        On.RainWorldGame.ctor += RainWorldGame_ctor;
    }

    private static string SaveState_SaveToString(On.SaveState.orig_SaveToString orig, SaveState self)
    {
        string save = orig(self);

        var saves = FindAllSaves(save).ToList();

        foreach (var pair in saves.OrderByDescending(x => x.Start))
            save = save.Remove(pair.Start, pair.End - pair.Start);

        save += SerializeToSave(Registries.Registrar.OnSave());
        UQLScribe.LInfo("Saved state!");
        return save;
    }

    private static void RainWorldGame_ctor(On.RainWorldGame.orig_ctor orig, RainWorldGame self, ProcessManager manager)
    {
        orig(self, manager);
        if (RequestLoadInGame(self) is {} data)
            Registries.Registrar.OnLoad(data, self);
    }
    
    private static IEnumerable<Wrapper>? RequestLoadInGame(RainWorldGame game)
    {
        if (!game.IsStorySession) 
        {
            UQLScribe.LWarn("Data load requested, but game is not in story session");
            return null;
        }
        return RequestLoad(game.StoryCharacter);
    }

    private static readonly UserData.FileDefinition expDefinition =
        (UserData.FileDefinition)typeof(PlayerProgression)
        .GetField("SAVE_FILE_EXP_DEFINITION", BindingFlags.NonPublic | BindingFlags.Static)
        .GetValue(null);
    
    private static readonly UserData.FileDefinition savDefinition =
        (UserData.FileDefinition)typeof(PlayerProgression)
        .GetField("SAVE_FILE_DEFINITION", BindingFlags.NonPublic | BindingFlags.Static)
        .GetValue(null);

    private static string? GetSaveString(SlugcatStats.Name saveNum)
    {
        RainWorld rainWorld = UQLScribe.rainWorldInstance;
        
        var filedef = new UserData.FileDefinition(
            (ModManager.Expedition && rainWorld.options.saveSlot < 0) 
            ? expDefinition : savDefinition);

        string filename = UQLScribe.rainWorldInstance.options.GetSaveFileName_SavOrExp();
        string? rawSave = KittehParse.LoadFile(filename, filedef);
        if (rawSave is null) return null;

        string? foundSave = KittehParse.FetchScugFromRaw(rawSave, saveNum);

        if (foundSave is null)
            UQLScribe.LWarn("Could not find save data for the current campaign");
        
        return foundSave;
    }
    internal static IEnumerable<Wrapper>? RequestLoad(SlugcatStats.Name saveNum)
    {
        string? foundSave = GetSaveString(saveNum);

        if (foundSave is null)
            return [];

        IEnumerable<Wrapper> parsedData = ParseFromSave(foundSave);
        return parsedData;
    }

    internal static Wrapper? RequestLoadSpecific(SlugcatStats.Name saveNum, string GUID, out bool success)
    {
        success = false;
        
        string? foundSave = GetSaveString(saveNum);

        if (foundSave is null) 
            return null;
        
        success = true;
        return ParseSpecific(foundSave, GUID);
    }
}
