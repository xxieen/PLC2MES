using System;
using System.Globalization;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using PLC2MES.Core.Models;

namespace PLC2MES.Utils
{
    public static class TypeConverter
    {
        public static object ConvertFromJson(object jsonValue, VariableType targetType, bool isArray = false)
        {
            if (jsonValue == null) return isArray ? new List<object>() : GetDefaultValue(targetType);
            try
            {
                if (isArray)
                {
                    // JsonElement / JsonNode array
                    if (jsonValue is JsonElement je && je.ValueKind == JsonValueKind.Array)
                    {
                        var list = new List<object>();
                        foreach (var item in je.EnumerateArray()) list.Add(ConvertScalarJsonElement(item, targetType));
                        return list;
                    }

                    if (jsonValue is JsonNode jn && jn is JsonArray jarr)
                    {
                        var list = new List<object>();
                        foreach (var item in jarr) list.Add(ConvertScalarObject(item, targetType));
                        return list;
                    }

                    // IEnumerable (already a collection)
                    if (jsonValue is IEnumerable<object> enumObj)
                    {
                        var list = new List<object>();
                        foreach (var it in enumObj) list.Add(ConvertFromJson(it, targetType, false));
                        return list;
                    }

                    // String that represents JSON array
                    var s = jsonValue.ToString().Trim();
                    if (s.StartsWith("[") && s.EndsWith("]"))
                    {
                        using var doc = JsonDocument.Parse(s);
                        var root = doc.RootElement;
                        var list = new List<object>();
                        foreach (var item in root.EnumerateArray()) list.Add(ConvertScalarJsonElement(item, targetType));
                        return list;
                    }

                    // Fallback: single value -> wrap
                    return new List<object> { ConvertFromJson(jsonValue, targetType, false) };
                }

                // scalar conversion
                return ConvertScalarObject(jsonValue, targetType);
            }
            catch
            {
                return isArray ? new List<object>() : GetDefaultValue(targetType);
            }
        }

        private static object ConvertScalarJsonElement(JsonElement element, VariableType targetType)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Number:
                    if (targetType == VariableType.Float)
                    {
                        if (element.TryGetDouble(out double d)) return d;
                        return Convert.ToDouble(element.GetRawText(), CultureInfo.InvariantCulture);
                    }
                    if (element.TryGetInt64(out long l)) return ConvertScalarToTarget(l, targetType);
                    if (element.TryGetDouble(out double dd)) return ConvertScalarToTarget(dd, targetType);
                    return GetDefaultValue(targetType);
                case JsonValueKind.String:
                    return ConvertScalarToTarget(element.GetString(), targetType);
                case JsonValueKind.True:
                case JsonValueKind.False:
                    return ConvertScalarToTarget(element.GetBoolean(), targetType);
                case JsonValueKind.Null:
                default:
                    return GetDefaultValue(targetType);
            }
        }

        private static object ConvertScalarObject(object value, VariableType targetType)
        {
            if (value is JsonElement je) return ConvertScalarJsonElement(je, targetType);
            if (value is JsonNode jn)
            {
                if (jn is JsonValue jv)
                {
                    try { var el = jv.GetValue<object>(); return ConvertScalarToTarget(el, targetType); } catch { return GetDefaultValue(targetType); }
                }
                return GetDefaultValue(targetType);
            }

            return ConvertScalarToTarget(value, targetType);
        }

        private static object ConvertScalarToTarget(object value, VariableType targetType)
        {
            if (value == null) return GetDefaultValue(targetType);

            try
            {
                switch (targetType)
                {
                    case VariableType.Bool:
                        if (value is bool b) return b;
                        if (value is string s)
                        {
                            if (bool.TryParse(s, out bool rb)) return rb;
                            if (long.TryParse(s, out long ln)) return ln != 0;
                        }
                        if (value is long ln2) return ln2 != 0;
                        if (value is double db2) return Math.Abs(db2) > double.Epsilon;
                        return Convert.ToBoolean(value);
                    case VariableType.Int:
                        if (value is long l) return l;
                        if (value is int i) return (long)i;
                        if (value is double d) return Convert.ToInt64(d);
                        if (value is string ss && long.TryParse(ss, out long rl)) return rl;
                        return Convert.ToInt64(value);
                    case VariableType.Float:
                        if (value is double dd) return dd;
                        if (value is float f) return (double)f;
                        if (value is string sfloat && double.TryParse(sfloat, NumberStyles.Any, CultureInfo.InvariantCulture, out double rf)) return rf;
                        return Convert.ToDouble(value);
                    case VariableType.String:
                        return value.ToString();
                    case VariableType.DateTime:
                        if (value is DateTime dt) return dt;
                        if (value is string sd && DateTime.TryParse(sd, out DateTime rd)) return rd;
                        return Convert.ToDateTime(value);
                    default:
                        return value;
                }
            }
            catch
            {
                return GetDefaultValue(targetType);
            }
        }

        public static string ConvertToJsonString(object value, VariableType type, bool isArray = false)
        {
            if (value == null) return isArray ? "[]" : "null";
            if (isArray)
            {
                if (value is IEnumerable e)
                {
                    var items = new List<string>();
                    foreach (var it in e)
                    {
                        items.Add(ConvertToJsonString(it, type, false));
                    }
                    return "[" + string.Join(",", items) + "]";
                }
                // fallback single
                return "[" + ConvertToJsonString(value, type, false) + "]";
            }

            switch (type)
            {
                case VariableType.Bool: return value.ToString().ToLower();
                case VariableType.Int:
                    return Convert.ToInt64(value).ToString(CultureInfo.InvariantCulture);
                case VariableType.Float:
                    return Convert.ToDouble(value).ToString(CultureInfo.InvariantCulture);
                case VariableType.String:
                    {
                        var s = value.ToString().Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
                        return "\"" + s + "\"";
                    }
                case VariableType.DateTime:
                    return "\"" + value.ToString() + "\"";
                default:
                    return "\"" + value.ToString() + "\"";
            }
        }

        public static object GetDefaultValue(VariableType type)
        {
            switch (type)
            {
                case VariableType.Bool: return false;
                case VariableType.Int: return 0L;
                case VariableType.Float: return 0.0;
                case VariableType.String: return string.Empty;
                case VariableType.DateTime: return DateTime.Now;
                default: return null;
            }
        }
    }
}
