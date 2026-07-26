namespace UQLScribe;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public static class UQLTag {
    public interface Element;
    public interface Compound : Tag;
    public interface Tag : Element;
    public interface IRecordEntry : Tag;

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

    private static string concatenate(IEnumerable<Compound> compounds)
    {
        StringBuilder sb = new();
        foreach (Compound c in compounds)
        {
            sb.Append(c);
            if (c is Record) sb.Append(','); // everything else is self-delimiting
        }
        if (sb.Length > 0 && sb[sb.Length-1] == ',') sb.Remove(sb.Length-1,1);
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

            if (this.entries.Length == 1 &&
                this.entries[0] is Label label &&
                label.val == "")
            {
                throw new ArgumentException("Sole entry of a Record cannot be an empty Label");
            }
        }

        public override string ToString()
        {
            return string.Join<IRecordEntry>(":",entries);
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
            return $"<{concatenate(compounds)}>";
        }
    }

    public class NamedGroup : Compound, IRecordEntry
    {
        public Label label;
        public Group group;

        public NamedGroup(Label label, Group group)
        {
            if (label.val == "") throw new ArgumentException("NamedGroup Label cannot be empty");
            this.label = label;
            this.group = group;
        }

        public override string ToString()
        {
            return label.ToString()+group.ToString();
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
            return $"<{label}:{concatenate(compounds)}>";
        }
    }

    private static class ParsingErrors {
        public static void Syntax(int chari,string text)
        {
            throw new ArgumentException($"Malformed UQLTag at char {chari}: {text}");
        }

        public static void SuddenEnd(int chari)
        {
            Syntax(chari, "Unexpected end of data");
        }
    }

    private enum ParseState
    {
        Label,
        Group,
        Record
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
        if (i == source.Length) ParsingErrors.SuddenEnd(i-1);
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
            return new NamedGroup(label, group);
        }
        return label;
    }

    private static Label ParseLabel(string source, ref int i)
    {
        StringBuilder label = new();
        for (;; i++)
        {
			if (i == source.Length) ParsingErrors.SuddenEnd(i-1);
            char c = source[i];
            if (Functional(c))
            {
                return new Label(label.ToString());
            }
			if (i + 1 == source.Length) ParsingErrors.SuddenEnd(i);
            if (c == '\\')
            {
                char d = source[i+1];
                if (Escaped(d)) {
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
        if (source[i] == '<') {
            entry = ParseGroup(source, ref i);
        } else
        {
            entry = ParseLabel(source, ref i);
            if (source[i] == '<') {
                Group group = ParseGroup(source, ref i);
                entry = new NamedGroup((Label) entry, group);
            }
        }
        if (source[i] == ':') return ParseRecord(entry, source, ref i);
        if (entry is Label label) return new Record([label]);
        return (Compound) entry;
    }
    private static Wrapper ParseWrapper(string source, ref int i)
    {
        if (source[i] != '<') ParsingErrors.Syntax(i, "Wrapper must start with \'<\'");

        i++;

        Label label = ParseLabel(source, ref i);

        if (source[i] != ':') ParsingErrors.Syntax(i, "Non-colon character found delimiting Wrapper Label");

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

    public static IEnumerable<Wrapper> ParseAll(string input)
    {
        for (int i = 0;i < input.Length;)
        {
            char c = input[i];
            if (c == '<') yield return ParseWrapper(input, ref i);
            else i++;
        }
    }
}