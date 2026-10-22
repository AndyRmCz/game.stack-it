using System.Collections.Generic;
using System.Text;

namespace StackIt.Client
{
    /// <summary>
    /// Source of truth for UI text: Resources/StackIt/strings.csv ("key,en,es", RFC 4180 quoting).
    /// The editor imports it into the Localization package's "UI" string table; at runtime it is
    /// also the fallback if the table is unavailable.
    /// </summary>
    public sealed class StringsCsv
    {
        private readonly Dictionary<string, Dictionary<string, string>> _byLanguage =
            new Dictionary<string, Dictionary<string, string>>();
        private readonly List<string> _keys = new List<string>();

        public IReadOnlyList<string> Languages { get; private set; } = new List<string>();
        public IEnumerable<string> Keys => _keys;

        public string Get(string language, string key) =>
            _byLanguage.TryGetValue(language, out var table) && table.TryGetValue(key, out string value) ? value : null;

        public static StringsCsv Parse(string csv)
        {
            var result = new StringsCsv();
            List<List<string>> rows = ReadRows(csv);
            if (rows.Count == 0) return result;

            var languages = new List<string>();
            for (int c = 1; c < rows[0].Count; c++)
            {
                languages.Add(rows[0][c].Trim());
                result._byLanguage[rows[0][c].Trim()] = new Dictionary<string, string>();
            }
            result.Languages = languages;

            for (int r = 1; r < rows.Count; r++)
            {
                List<string> row = rows[r];
                string key = row[0].Trim();
                if (key.Length == 0 || key.StartsWith("#")) continue;
                result._keys.Add(key);
                for (int c = 1; c < row.Count && c <= languages.Count; c++)
                    result._byLanguage[languages[c - 1]][key] = row[c];
            }
            return result;
        }

        private static List<List<string>> ReadRows(string csv)
        {
            var rows = new List<List<string>>();
            var row = new List<string>();
            var field = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < csv.Length; i++)
            {
                char c = csv[i];
                if (quoted)
                {
                    if (c == '"')
                    {
                        if (i + 1 < csv.Length && csv[i + 1] == '"')
                        {
                            field.Append('"');
                            i++;
                        }
                        else quoted = false;
                    }
                    else if (c != '\r') field.Append(c);
                    continue;
                }
                switch (c)
                {
                    case '"':
                        quoted = true;
                        break;
                    case ',':
                        row.Add(field.ToString());
                        field.Clear();
                        break;
                    case '\r':
                        break;
                    case '\n':
                        row.Add(field.ToString());
                        field.Clear();
                        if (row.Count > 1 || row[0].Length > 0) rows.Add(row);
                        row = new List<string>();
                        break;
                    default:
                        field.Append(c);
                        break;
                }
            }
            if (field.Length > 0 || row.Count > 0)
            {
                row.Add(field.ToString());
                rows.Add(row);
            }
            return rows;
        }
    }
}
