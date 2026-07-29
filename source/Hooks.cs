using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Kittehface.Framework20;

namespace UQLScribe;

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

        var saves = UQLTag.FindAllSaves(save).ToList();

        foreach (var pair in saves.OrderByDescending(x => x.Start))
        {
            save = save.Remove(pair.Start, pair.End - pair.Start);
        }

        save += UQLTag.SerializeToSave(Registries.Registrar.OnSave());
        UQLScribe.LInfo("Saved state!");
        return save;
    }

    private static void RainWorldGame_ctor(On.RainWorldGame.orig_ctor orig, RainWorldGame self, ProcessManager manager)
    {
        orig(self, manager);
        if (RequestLoadInGame(self, out var saveData))
            Registries.Registrar.OnLoad(saveData, self);
    }
    
    private static bool RequestLoadInGame(RainWorldGame game, out IEnumerable<UQLTag.Wrapper> saveData)
    {
        saveData = null;
        if (!game.IsStorySession) 
        {
            UQLScribe.LWarn("Data load requested, but game is not in story session");
            return false;
        }
        return RequestLoad(game.StoryCharacter, out saveData);
    }
    internal static bool RequestLoad(SlugcatStats.Name saveNum, out IEnumerable<UQLTag.Wrapper> saveData)
    {
        saveData = null;
        RainWorld rainWorld = UQLScribe.rainWorldInstance;
        
        FieldInfo expField = typeof(PlayerProgression).GetField("SAVE_FILE_EXP_DEFINITION", 
            BindingFlags.NonPublic | BindingFlags.Static);
        var exp = (UserData.FileDefinition)expField.GetValue(null);

        FieldInfo normField = typeof(PlayerProgression).GetField("SAVE_FILE_DEFINITION", 
            BindingFlags.NonPublic | BindingFlags.Static);
        var norm = (UserData.FileDefinition)normField.GetValue(null);

        var filedef = new UserData.FileDefinition(
            (ModManager.Expedition && rainWorld.options.saveSlot < 0) ? exp : norm);

        string filename = UQLScribe.rainWorldInstance.options.GetSaveFileName_SavOrExp();
        string? rawSave = KittehParse.LoadFile(filename, filedef);
        if (rawSave == null) return false;

        string? foundSave = KittehParse.fetchScugFromRaw(rawSave, saveNum);

        if (foundSave == null)
        {
            UQLScribe.LWarn("Could not find save data for the current campaign");
            saveData = Enumerable.Empty<UQLTag.Wrapper>();
            return true;
        }
        IEnumerable<UQLTag.Wrapper> parsedData = UQLTag.ParseFromSave(foundSave);
        saveData = parsedData;
        return true;
    }
}
