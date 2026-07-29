namespace UQLScribe;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using Kittehface.Framework20;

public static class KittehParse
{
    public static Dictionary<string, string>? Deserialize(byte[] serializedData)
    {
        var extractedData = new Dictionary<string, string>();

        try
        {
            XmlDocument xmlDocument = new XmlDocument();
            using (MemoryStream input = new MemoryStream(serializedData))
            {
                XmlTextReader xmlTextReader = new XmlTextReader(input)
                {
                    Namespaces = false
                };
                xmlDocument.Load(xmlTextReader);
            }

            if (xmlDocument.SelectSingleNode("//Keys") != null)
            {
                XmlNodeList keysList = xmlDocument.SelectSingleNode("//Keys").ChildNodes;
                XmlNodeList valuesList = xmlDocument.SelectSingleNode("//Values").ChildNodes;

                for (int i = 0; i < keysList.Count && i < valuesList.Count; i++)
                {
                    extractedData[keysList[i].InnerText] = valuesList[i].InnerText;
                }
            }

            else if (xmlDocument.SelectSingleNode("//KeyValueOfanyTypeanyType") != null)
            {
                XmlNodeList pairNodes = xmlDocument.SelectNodes("//KeyValueOfanyTypeanyType");

                for (int j = 0; j < pairNodes.Count; j++)
                {
                    XmlNode keyNode = pairNodes[j].SelectSingleNode("Key");
                    XmlNode valueNode = pairNodes[j].SelectSingleNode("Value");

                    if (keyNode != null && valueNode != null)
                    {
                        extractedData[keyNode.InnerText] = valueNode.InnerText;
                    }
                }
            }
            else
            {
                throw new InvalidDataException("Data was not a recognized serialized data format.");
            }
        }
        catch (Exception ex)
        {
            UQLScribe.LoggerInstance.LogError($"KittehParse: Exception Deserializing Data: {ex.Message}");
            return null;
        }

        return extractedData;
    }

    public static byte[] TruncateNullBytes(byte[] input)
    {
        for (int num = input.Length - 1; num >= 0; num--)
        {
            if (input[num] != 0)
            {
                if (num == input.Length - 1)
                {
                    return input;
                }

                byte[] array = new byte[num + 1];
                Array.Copy(input, array, num + 1);
                return array;
            }
        }

        return input;
    }

    public static string? LoadFile(string filename, UserData.FileDefinition filedef, bool extractBackup = false)
    {
        try
        {
            string dir = UserData.GetPersistentDataPath();
            string exactName = UserData.GetFilenameToUse(filename, filedef);
            string absolutePath = Path.Combine(dir, exactName);

            if (!File.Exists(absolutePath))
            {
                UQLScribe.LoggerInstance.LogWarning($"KittehParse: File not found at: {absolutePath}");
                return null;
            }

            byte[] bytes = TruncateNullBytes(File.ReadAllBytes(absolutePath));

            Dictionary<string, string>? parsedMap = Deserialize(bytes);
            if (parsedMap == null || parsedMap.Count == 0)
            {
                UQLScribe.LoggerInstance.LogError($"KittehParse: Failed to deserialize or extract keys from {exactName}");
                return null;
            }

            string targetKey = extractBackup ? "save__Backup" : "save";

            if (parsedMap.TryGetValue(targetKey, out string? payload))
            {
                return payload;
            }

            UQLScribe.LoggerInstance.LogError($"KittehParse: Expected key '{targetKey}' was missing from the deserialized structure.");

        }
        catch (Exception ex)

        {
            UQLScribe.LoggerInstance.LogError($"KittehParse: Critical exception in LoadFile: {ex.Message}");
        }

        return null;
    }

    public static string? fetchScugFromRaw(string rawSave, SlugcatStats.Name name)
    {
        string prefix = "<progDivA>SAVE STATE";
        string endfix = "<progDivA>";
        string prelude = "<progDivB>SAV STATE NUMBER<svB>";
        int start;
        for(int i = 0; i + prefix.Length <= rawSave.Length; i++)
        {
            if (rawSave.Substring(i,prefix.Length) == prefix)
            {
                i += prefix.Length;
                start = i;
                i += prelude.Length;
                StringBuilder nameSB = new();
                for(;i < rawSave.Length && rawSave[i] != '<';i++)
                {
                    nameSB.Append(rawSave[i]);
                }
                if (nameSB.ToString() == name.ToString())
                {
                    int j = 0;
                    for(; i + j + endfix.Length <= rawSave.Length; j++) {
                        if (rawSave.Substring(i+j,endfix.Length) == endfix)
                            break;
                    }
                    return rawSave.Substring(start,i+j-start);
                }
            }
        }
        return null;
    }
}