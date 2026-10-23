using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace StackIt.Client.Theming
{
    /// <summary>
    /// Just enough JSON for themes.json (this assembly has no JSON library): objects become
    /// Dictionary&lt;string, object&gt;, arrays List&lt;object&gt;, numbers double, plus string, bool and null.
    /// </summary>
    public static class MiniJson
    {
        public static object Parse(string json)
        {
            int i = 0;
            object value = Value(json, ref i);
            Skip(json, ref i);
            if (i != json.Length) throw Error(json, i, "trailing characters");
            return value;
        }

        private static object Value(string s, ref int i)
        {
            Skip(s, ref i);
            if (i >= s.Length) throw Error(s, i, "unexpected end");
            char c = s[i];
            if (c == '{') return Obj(s, ref i);
            if (c == '[') return Arr(s, ref i);
            if (c == '"') return Str(s, ref i);
            if (c == 't' && Word(s, ref i, "true")) return true;
            if (c == 'f' && Word(s, ref i, "false")) return false;
            if (c == 'n' && Word(s, ref i, "null")) return null;
            return Num(s, ref i);
        }

        private static Dictionary<string, object> Obj(string s, ref int i)
        {
            var result = new Dictionary<string, object>();
            i++;
            Skip(s, ref i);
            if (s[i] == '}') { i++; return result; }
            while (true)
            {
                Skip(s, ref i);
                string key = Str(s, ref i);
                Skip(s, ref i);
                if (s[i] != ':') throw Error(s, i, "expected ':'");
                i++;
                result[key] = Value(s, ref i);
                Skip(s, ref i);
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; return result; }
                throw Error(s, i, "expected ',' or '}'");
            }
        }

        private static List<object> Arr(string s, ref int i)
        {
            var result = new List<object>();
            i++;
            Skip(s, ref i);
            if (s[i] == ']') { i++; return result; }
            while (true)
            {
                result.Add(Value(s, ref i));
                Skip(s, ref i);
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; return result; }
                throw Error(s, i, "expected ',' or ']'");
            }
        }

        private static string Str(string s, ref int i)
        {
            if (s[i] != '"') throw Error(s, i, "expected a string");
            var sb = new StringBuilder();
            i++;
            while (s[i] != '"')
            {
                char c = s[i++];
                if (c != '\\') { sb.Append(c); continue; }
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        i += 4;
                        break;
                    default: sb.Append(e); break; // \" \\ \/
                }
            }
            i++;
            return sb.ToString();
        }

        private static double Num(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            if (start == i) throw Error(s, i, "unexpected character");
            return double.Parse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private static bool Word(string s, ref int i, string word)
        {
            if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0) return false;
            i += word.Length;
            return true;
        }

        private static void Skip(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        private static FormatException Error(string s, int i, string what) =>
            new FormatException($"JSON: {what} at character {i}.");
    }
}
