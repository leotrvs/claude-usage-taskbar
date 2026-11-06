using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Web.Script.Serialization;

static class Json
{
    public static object Parse(string text)
    {
        JavaScriptSerializer serializer = new JavaScriptSerializer();
        serializer.MaxJsonLength = int.MaxValue;
        return serializer.DeserializeObject(text);
    }

    public static Dictionary<string, object> Object(object value)
    {
        return value as Dictionary<string, object>;
    }

    public static object Get(Dictionary<string, object> source, string key)
    {
        object value;
        return source != null && source.TryGetValue(key, out value) ? value : null;
    }

    // Keeps a user's file readable: key order is preserved and it is indented like Claude Code's own output.
    public static string Format(object value)
    {
        StringBuilder output = new StringBuilder();
        Write(output, value, 0);
        output.Append('\n');
        return output.ToString();
    }

    static void Write(StringBuilder output, object value, int depth)
    {
        Dictionary<string, object> map = value as Dictionary<string, object>;
        if (value == null) output.Append("null");
        else if (value is bool) output.Append((bool)value ? "true" : "false");
        else if (value is string) WriteString(output, (string)value);
        else if (map != null) WriteObject(output, map, depth);
        else if (value is IEnumerable) WriteArray(output, (IEnumerable)value, depth);
        else output.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
    }

    static void WriteObject(StringBuilder output, Dictionary<string, object> map, int depth)
    {
        if (map.Count == 0) { output.Append("{}"); return; }
        output.Append('{');
        bool first = true;
        foreach (KeyValuePair<string, object> pair in map)
        {
            output.Append(first ? "\n" : ",\n").Append(' ', (depth + 1) * 2);
            WriteString(output, pair.Key);
            output.Append(": ");
            Write(output, pair.Value, depth + 1);
            first = false;
        }
        output.Append('\n').Append(' ', depth * 2).Append('}');
    }

    static void WriteArray(StringBuilder output, IEnumerable items, int depth)
    {
        bool first = true;
        output.Append('[');
        foreach (object item in items)
        {
            output.Append(first ? "\n" : ",\n").Append(' ', (depth + 1) * 2);
            Write(output, item, depth + 1);
            first = false;
        }
        if (first) { output.Append(']'); return; }
        output.Append('\n').Append(' ', depth * 2).Append(']');
    }

    static void WriteString(StringBuilder output, string text)
    {
        output.Append('"');
        foreach (char c in text)
        {
            switch (c)
            {
                case '"': output.Append("\\\""); break;
                case '\\': output.Append("\\\\"); break;
                case '\n': output.Append("\\n"); break;
                case '\r': output.Append("\\r"); break;
                case '\t': output.Append("\\t"); break;
                default:
                    if (c < ' ') output.AppendFormat("\\u{0:x4}", (int)c);
                    else output.Append(c);
                    break;
            }
        }
        output.Append('"');
    }
}
