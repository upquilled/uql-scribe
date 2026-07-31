namespace UQLScribe;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public static class UQLTag
{

    public class UQLTagSyntaxException : Exception
    {
        public UQLTagSyntaxException(string message) : base(message) { }
    }
    public interface Element;
    public interface Compound : Tag;
    public interface Tag : Element;
    public interface IRecordEntry : Tag;

    private static string prefix = "[scribesaves:";
    private static char suffix = ']';

    private static bool Functional(char c)
    {
        switch (c)
        {
            case ':':
            case '<':
            case '>':
            case ',':
                return true;
            default:
                return false;
        }
    }

    private static bool Escaped(char c)
    {
        return Functional(c) || c == '\\';
    }

    private static string serializeToGroup(IEnumerable<Compound> compounds)
    {
        StringBuilder sb = new();
        Compound? prev = null;
        Compound? final = null;
        foreach (Compound c in compounds)
        {
            sb.Append(prev);
            if (prev is Record || // everything is self-delimiting 
               ((prev is NamedGroup || prev is Group)
                && c is Record record
                && record.entries.Length >= 1
                && record.entries[0] is Label label
                && label.val == "")) // resolve ambiguity <...>:B vs. <...>,:B
                sb.Append(',');
            prev = c;
            final = c;
        }
        sb.Append(final);
        return sb.ToString();
    }
    public class Label : Tag, IRecordEntry
    {
        public string val;
        public Label(string s)
        {
            val = s;
        }
        public override string ToString()
        {
            StringBuilder sb = new();
            for (int i = 0; i < val.Length; i++)
            {
                char c = val[i];

                if (Escaped(c))
                    sb.Append("\\");

                sb.Append(c);
            }

            return sb.ToString();
        }
    }

    public class Record : Compound
    {
        public IRecordEntry[] entries;

        public Record(IEnumerable<IRecordEntry> entries)
        {
            this.entries = entries.ToArray();

            if (this.entries.Length == 1)
            {
                if (this.entries[0] is not Label label)
                    throw new ArgumentException($"Sole entry of a Record must be a Label, not a {this.entries[0].GetType().Name}");
                
                if (label.val == "")
                    throw new ArgumentException("Sole entry of a Record cannot be an empty Label");
            }
        }

        public override string ToString()
        {
            return string.Join<IRecordEntry>(":", entries);
        }
    }

    public class Group : Compound, IRecordEntry
    {
        public Compound[] compounds;

        public Group(IEnumerable<Compound> compounds)
        {
            this.compounds = compounds.ToArray();
        }

        public override string ToString()
        {
            return $"<{serializeToGroup(compounds)}>";
        }
    }

    public class NamedGroup : Compound, IRecordEntry
    {
        public Label label;
        public Compound[] compounds;

        public NamedGroup(Label label, IEnumerable<Compound> compounds)
        {
            if (label.val == "") throw new ArgumentException("NamedGroup Label cannot be empty");
            this.label = label;
            this.compounds = compounds.ToArray();
        }

        public override string ToString()
        {
            return $"{label}<{serializeToGroup(compounds)}>";
        }
    }

    public class Wrapper : Element
    {
        public Label label;

        public Compound[] compounds;

        public Wrapper(Label label, IEnumerable<Compound> compounds)
        {
            this.label = label;
            this.compounds = compounds.ToArray();
        }

        public override string ToString()
        {
            return $"<{label}:{serializeToGroup(compounds)}>";
        }
    }

    private static class ParsingErrors
    {
        public static void Syntax(int chari, string text)
        {
            throw new UQLTagSyntaxException($"Malformed UQLTag at char {chari}: {text}");
        }

        public static void SuddenEnd(int chari)
        {
            Syntax(chari, "Unexpected end of data");
        }
    }

    private static Group ParseGroup(string source, ref int i)
    {
        i++;
        List<Compound> elements = new();
        while (source[i] != '>')
        {
            elements.Add(ParseGroupElement(source, ref i));
            if (source[i] == ',') i++;
        }
        i++;
        if (i == source.Length) ParsingErrors.SuddenEnd(i - 1);
        return new Group(elements);
    }

    private static Record ParseRecord(IRecordEntry firstEntry, string source, ref int i)
    {
        List<IRecordEntry> records = [firstEntry];
        while (source[i] == ':')
        {
            i++;
            records.Add(ParseRecordEntry(source, ref i));
        }
        return new Record(records);
    }

    private static IRecordEntry ParseRecordEntry(string source, ref int i)
    {
        if (source[i] == '<') return ParseGroup(source, ref i);
        Label label = ParseLabel(source, ref i);
        if (source[i] == '<')
        {
            Group group = ParseGroup(source, ref i);
            return new NamedGroup(label, group.compounds);
        }
        return label;
    }

    private static Label ParseLabel(string source, ref int i)
    {
        StringBuilder label = new();
        for (; ; i++)
        {
            if (i == source.Length) ParsingErrors.SuddenEnd(i - 1);
            char c = source[i];
            if (Functional(c))
            {
                return new Label(label.ToString());
            }
            if (i + 1 == source.Length) ParsingErrors.SuddenEnd(i);
            if (c == '\\')
            {
                char d = source[i + 1];
                if (Escaped(d))
                {
                    label.Append(d);
                    i++;
                    continue;
                }
            }
            label.Append(c);
        }
    }
    private static Compound ParseGroupElement(string source, ref int i)
    {
        IRecordEntry entry;
        if (source[i] == '<')
        {
            entry = ParseGroup(source, ref i);
        }
        else
        {
            entry = ParseLabel(source, ref i);
            if (source[i] == '<')
            {
                Group group = ParseGroup(source, ref i);
                entry = new NamedGroup((Label)entry, group.compounds);
            }
        }
        if (source[i] == ':') return ParseRecord(entry, source, ref i);
        if (entry is Label label) return new Record([label]);
        return (Compound)entry;
    }
    private static Wrapper ParseWrapper(string source, ref int i)
    {
        if (i >= source.Length) ParsingErrors.SuddenEnd(i);
        if (source[i] != '<') ParsingErrors.Syntax(i, "Wrapper must start with \'<\'");

        i++;

        Label label = ParseLabel(source, ref i);

        if (source[i] != ':') ParsingErrors.Syntax(i, "Non-colon character found "
                                                    + "delimiting Wrapper Label");

        i++;

        List<Compound> elements = new();

        while (source[i] != '>')
        {
            elements.Add(ParseGroupElement(source, ref i));
            if (source[i] == ',') i++;
            if (i == source.Length) ParsingErrors.SuddenEnd(i);
        }
        i++;
        return new Wrapper(label, elements);
    }

    public static Wrapper Parse(string input)
    {
        int i = 0;
        return ParseWrapper(input, ref i);
    }

    private static bool WalkWrapperOrReturn(string input, ref int i, string targetGUID, out Wrapper? wrapper)
    {
        wrapper = null;

        int j = i;

        if (input[j] == ']') 
            return false;
        
        if (input[j] != '<')
        {
                UQLScribe.LError("Expected to find '<' at start of save "
                              + $"Wrapper, but found '{input[j]}'!");
            return false;
        }

        j += 1;
        
        Label label = ParseLabel(input, ref j);

        if (label.val == targetGUID)
        {
            wrapper = ParseWrapper(input, ref i);
            return true;
        }
        
        int depth = 1;

        for (;;j++)
        {
            if (j >= input.Length)
            {
                UQLScribe.LError("Savestring ends on incomplete Wrapper!");
                return false;
            }
            char c = input[j];
            switch(c)
            {
                case '<':
                    depth += 1;
                    break;
                case '>':
                    depth -= 1;
                    break;
                case '\\':
                    j++; // skip escaped char
                    continue;
            }
            if (depth == 0)
            {
                i = j + 1;
                return true;
            }
        }
    }

    internal static Wrapper? ParseSpecific(string input, string GUID)
    {
        int i = input.IndexOf(prefix);
        i += prefix.Length;
        while (WalkWrapperOrReturn(input, ref i, GUID, out var wrapper))
            if (wrapper is not null)
                return wrapper;
        return null;
    }

    internal static IEnumerable<Wrapper> ParseFromSave(string input)
    {
        int i = input.IndexOf(prefix);
        if (i == -1)
        {
            UQLScribe.LWarn("No save data block found!");
            yield break;
        }
        i += prefix.Length;
        if (i >= input.Length)
        {
            UQLScribe.LError("Save data prefix found, but no data follows it");
            yield break;
        }
        while (i < input.Length && input[i] == '<')
        {
            Wrapper? save = null;
            try
            {
                save = ParseWrapper(input, ref i);
            }
            catch (UQLTagSyntaxException e)
            {
                UQLScribe.LError(e.Message);
            }
            if (save is not null) yield return save;
            else
            {
                yield break;
            }
        }
        if (i >= input.Length || input[i] != suffix)
            UQLScribe.LWarn("No termination suffix found! "
                          + "Save data may be malformed");
    }

    internal static string SerializeToSave(IEnumerable<Wrapper> input)
    {
        StringBuilder sb = new();
        sb.Append(prefix);
        foreach (Wrapper x in input)
            sb.Append(x);
        sb.Append(suffix);
        return sb.ToString();
    }

    internal static IEnumerable<(int Start, int End)> FindAllSaves(string input)
    {
        int? start = null;
        int? depth = null;

        for (int i = 0; i + prefix.Length <= input.Length; i++)
        {
            if (string.Compare(input, i, prefix, 0, prefix.Length, 
                StringComparison.Ordinal) == 0)
            {
                start = i;
                i += prefix.Length - 1;
                depth = 0;
                continue;
            }
            if (depth is not null)
            {
                char c = input[i];
                if (depth == 0)
                {
                    if (c == ']')
                    {
                        yield return ((int)start!, i + 1);
                        depth = start = null;
                        continue;
                    }
                    else if (c != '<')
                    {
                        UQLScribe.LInfo("Block appears to be malformed. "
                                      + "Continuing to next...");
                        depth = start = null;
                        i--;
                        continue;
                    }
                }
                switch (c)
                {
                    case '\\':
                        i++; // skip escaped char
                        continue;
                    case '<':
                        depth += 1;
                        continue;
                    case '>':
                        depth -= 1;
                        continue;
                }
            }
        }
    }
}