using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace OpenRazerProfiler.Protocolo
{
    // Objeto JSON que preserva a ordem das chaves.
    public sealed class JsonObject : IEnumerable<KeyValuePair<string, object>>
    {
        readonly List<KeyValuePair<string, object>> items = new List<KeyValuePair<string, object>>();

        public object this[string key]
        {
            get => TryGet(key, out var v) ? v : null;
            set
            {
                int i = items.FindIndex(kv => kv.Key == key);
                if (i >= 0)
                    items[i] = new KeyValuePair<string, object>(key, value);
                else
                    items.Add(new KeyValuePair<string, object>(key, value));
            }
        }

        public void Add(string key, object value) => this[key] = value;

        public bool TryGet(string key, out object value)
        {
            foreach (var kv in items)
                if (kv.Key == key)
                {
                    value = kv.Value;
                    return true;
                }
            value = null;
            return false;
        }

        public string String(string key) => this[key] as string ?? throw new FormatException($"Campo \"{key}\" ausente ou não é texto.");

        public long Number(string key) =>
            this[key] is long l ? l : this[key] is double d ? (long)d : throw new FormatException($"Campo \"{key}\" ausente ou não é número.");

        public JsonObject Object(string key) => this[key] as JsonObject ?? throw new FormatException($"Campo \"{key}\" ausente ou não é objeto.");

        public List<object> Array(string key) => this[key] as List<object> ?? throw new FormatException($"Campo \"{key}\" ausente ou não é lista.");

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => items.GetEnumerator();
    }

    // Leitor e escritor JSON mínimos, sem dependências externas.
    public static class Json
    {
        public static string Write(object value)
        {
            var sb = new StringBuilder();
            WriteValue(sb, value, 0);
            sb.Append('\n');
            return sb.ToString();
        }

        static void WriteValue(StringBuilder sb, object value, int indent)
        {
            switch (value)
            {
                case null:
                    sb.Append("null");
                    break;
                case string s:
                    WriteString(sb, s);
                    break;
                case bool b:
                    sb.Append(b ? "true" : "false");
                    break;
                case int i:
                    sb.Append(i.ToString(CultureInfo.InvariantCulture));
                    break;
                case long l:
                    sb.Append(l.ToString(CultureInfo.InvariantCulture));
                    break;
                case double d:
                    sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
                    break;
                case JsonObject o:
                    WriteObject(sb, o, indent);
                    break;
                case IEnumerable list:
                    WriteArray(sb, list, indent);
                    break;
                default:
                    throw new ArgumentException($"Tipo sem conversão para JSON: {value.GetType().Name}");
            }
        }

        static void WriteObject(StringBuilder sb, JsonObject o, int indent)
        {
            // Objetos grandes só com valores simples (ex.: o mapa de teclas) ocupam várias entradas por linha.
            bool packed = IsCompact(o);
            sb.Append('{');
            bool first = true;
            int lineStart = sb.Length;
            foreach (var kv in o)
            {
                if (!first)
                    sb.Append(',');
                if (!packed || first || sb.Length - lineStart > 96)
                {
                    NewLine(sb, indent + 1);
                    lineStart = sb.Length - (indent + 1) * 2;
                }
                else
                {
                    sb.Append(' ');
                }
                WriteString(sb, kv.Key);
                sb.Append(": ");
                WriteValue(sb, kv.Value, indent + 1);
                first = false;
            }
            if (!first)
                NewLine(sb, indent);
            sb.Append('}');
        }

        static bool IsCompact(JsonObject o)
        {
            int count = 0;
            foreach (var kv in o)
            {
                if (kv.Value is JsonObject || (kv.Value is IEnumerable && !(kv.Value is string)))
                    return false;
                count++;
            }
            return count > 6;
        }

        static void WriteArray(StringBuilder sb, IEnumerable list, int indent)
        {
            sb.Append('[');
            bool first = true;
            foreach (var item in list)
            {
                sb.Append(first ? "" : ",");
                NewLine(sb, indent + 1);
                WriteValue(sb, item, indent + 1);
                first = false;
            }
            if (!first)
                NewLine(sb, indent);
            sb.Append(']');
        }

        static void NewLine(StringBuilder sb, int indent) => sb.Append('\n').Append(' ', indent * 2);

        static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                            sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else
                            sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        public static object Parse(string text)
        {
            int pos = 0;
            var value = ParseValue(text, ref pos);
            SkipSpace(text, ref pos);
            if (pos != text.Length)
                throw Error(text, pos, "conteúdo depois do fim");
            return value;
        }

        static object ParseValue(string t, ref int pos)
        {
            SkipSpace(t, ref pos);
            if (pos >= t.Length)
                throw Error(t, pos, "fim inesperado");
            char c = t[pos];
            if (c == '{')
                return ParseObject(t, ref pos);
            if (c == '[')
                return ParseArray(t, ref pos);
            if (c == '"')
                return ParseString(t, ref pos);
            if (c == '-' || char.IsDigit(c))
                return ParseNumber(t, ref pos);
            if (Match(t, ref pos, "true"))
                return true;
            if (Match(t, ref pos, "false"))
                return false;
            if (Match(t, ref pos, "null"))
                return null;
            throw Error(t, pos, $"caractere inesperado '{c}'");
        }

        static JsonObject ParseObject(string t, ref int pos)
        {
            var o = new JsonObject();
            pos++;
            SkipSpace(t, ref pos);
            if (pos < t.Length && t[pos] == '}')
            {
                pos++;
                return o;
            }
            while (true)
            {
                SkipSpace(t, ref pos);
                if (pos >= t.Length || t[pos] != '"')
                    throw Error(t, pos, "esperava o nome de um campo");
                string key = ParseString(t, ref pos);
                SkipSpace(t, ref pos);
                if (pos >= t.Length || t[pos] != ':')
                    throw Error(t, pos, "esperava ':'");
                pos++;
                o[key] = ParseValue(t, ref pos);
                SkipSpace(t, ref pos);
                if (pos < t.Length && t[pos] == ',')
                {
                    pos++;
                    continue;
                }
                if (pos < t.Length && t[pos] == '}')
                {
                    pos++;
                    return o;
                }
                throw Error(t, pos, "esperava ',' ou '}'");
            }
        }

        static List<object> ParseArray(string t, ref int pos)
        {
            var list = new List<object>();
            pos++;
            SkipSpace(t, ref pos);
            if (pos < t.Length && t[pos] == ']')
            {
                pos++;
                return list;
            }
            while (true)
            {
                list.Add(ParseValue(t, ref pos));
                SkipSpace(t, ref pos);
                if (pos < t.Length && t[pos] == ',')
                {
                    pos++;
                    continue;
                }
                if (pos < t.Length && t[pos] == ']')
                {
                    pos++;
                    return list;
                }
                throw Error(t, pos, "esperava ',' ou ']'");
            }
        }

        static string ParseString(string t, ref int pos)
        {
            var sb = new StringBuilder();
            pos++;
            while (pos < t.Length)
            {
                char c = t[pos++];
                if (c == '"')
                    return sb.ToString();
                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }
                if (pos >= t.Length)
                    break;
                char e = t[pos++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (pos + 4 > t.Length)
                            throw Error(t, pos, "escape \\u incompleto");
                        sb.Append((char)Convert.ToInt32(t.Substring(pos, 4), 16));
                        pos += 4;
                        break;
                    default:
                        throw Error(t, pos, $"escape inválido \\{e}");
                }
            }
            throw Error(t, pos, "texto sem fim");
        }

        static object ParseNumber(string t, ref int pos)
        {
            int start = pos;
            while (pos < t.Length && "+-0123456789.eE".IndexOf(t[pos]) >= 0)
                pos++;
            string s = t.Substring(start, pos - start);
            if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l))
                return l;
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                return d;
            throw Error(t, start, $"número inválido '{s}'");
        }

        static bool Match(string t, ref int pos, string word)
        {
            if (string.CompareOrdinal(t, pos, word, 0, word.Length) != 0)
                return false;
            pos += word.Length;
            return true;
        }

        static void SkipSpace(string t, ref int pos)
        {
            while (pos < t.Length && char.IsWhiteSpace(t[pos]))
                pos++;
        }

        static FormatException Error(string t, int pos, string what)
        {
            int line = 1;
            for (int i = 0; i < pos && i < t.Length; i++)
                if (t[i] == '\n')
                    line++;
            return new FormatException($"JSON inválido na linha {line}: {what}.");
        }
    }
}
