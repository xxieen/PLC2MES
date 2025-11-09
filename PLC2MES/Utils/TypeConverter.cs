using System;
using System.Globalization;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using PLC2MES.Core.Models;

namespace PLC2MES.Utils
{
    public static class TypeConverter
    {
        // Convert input (JsonNode, string, IEnumerable, scalar) to CLR value according to targetType.
        // If targetType.IsArray==true, element type is targetType.ElementType
        public static object ConvertFromJson(object input, VariableType targetType)
        {
            if (targetType == null) return input;
            bool isArray = targetType.IsArray;
            var elemType = isArray ? targetType.ElementType ?? VariableType.CreateScalar(VariableKind.String) : targetType;

            if (input == null) return isArray ? new List<object>() : GetDefaultValue(elemType);

            try
            {
                if (isArray)
                {
                    // JsonNode array
                    if (input is JsonArray ja)
                    {
                        var outList = new List<object>();
                        foreach (var it in ja) outList.Add(ConvertJsonNodeToClr(it, elemType));
                        return outList;
                    }

                    if (input is JsonNode jn && jn is JsonArray jnArr)
                    {
                        var outList = new List<object>();
                        foreach (var it in jnArr) outList.Add(ConvertJsonNodeToClr(it, elemType));
                        return outList;
                    }

                    if (input is string s)
                    {
                        var t = s.Trim();
                        if (t.StartsWith("[") && t.EndsWith("]"))
                        {
                            var parsed = JsonNode.Parse(t) as JsonArray;
                            if (parsed != null)
                            {
                                var outList = new List<object>();
                                foreach (var it in parsed) outList.Add(ConvertJsonNodeToClr(it, elemType));
                                return outList;
                            }
                        }
                    }

                    if (input is IEnumerable ie && !(input is string))
                    {
                        var outList = new List<object>();
                        foreach (var it in ie) outList.Add(ConvertFromJson(it, elemType));
                        return outList;
                    }

                    return new List<object> { ConvertFromJson(input, elemType) };
                }

                // Non-array handling
                if (input is JsonNode node)
                {
                    if (node is JsonObject jo) return ConvertJsonObjectToDict(jo);
                    if (node is JsonArray ja2)
                    {
                        var outList = new List<object>();
                        foreach (var it in ja2) outList.Add(ConvertJsonNodeToClr(it, elemType));
                        return outList;
                    }

                    return ConvertJsonNodeToClr(node, elemType);
                }

                if (input is string str)
                {
                    var trimmed = str.Trim();
                    if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
                    {
                        var parsed = JsonNode.Parse(trimmed) as JsonObject;
                        if (parsed != null) return ConvertJsonObjectToDict(parsed);
                    }

                    if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                    {
                        var parsed = JsonNode.Parse(trimmed) as JsonArray;
                        if (parsed != null)
                        {
                            var outList = new List<object>();
                            foreach (var it in parsed) outList.Add(ConvertJsonNodeToClr(it, elemType));
                            return outList;
                        }
                    }
                }

                // scalar conversion
                return ConvertScalarToTarget(input, elemType);
            }
            catch
            {
                return isArray ? new List<object>() : GetDefaultValue(elemType);
            }
        }

        // Convert a JsonNode (value/object/array) or scalar to CLR according to targetType
        private static object ConvertJsonNodeToClr(JsonNode node, VariableType targetType)
        {
            if (node == null) return GetDefaultValue(targetType);
            if (node is JsonObject jo) return ConvertJsonObjectToDict(jo);
            if (node is JsonArray ja)
            {
                var outList = new List<object>();
                foreach (var it in ja) outList.Add(ConvertJsonNodeToClr(it, targetType));
                return outList;
            }

            if (node is JsonValue jv)
            {
                try
                {
                    var val = jv.GetValue<object>();
                    return ConvertScalarToTarget(val, targetType);
                }
                catch
                {
                    return GetDefaultValue(targetType);
                }
            }

            // fallback
            return ConvertScalarToTarget(node.ToJsonString(), targetType);
        }

        // Convert a JsonObject to Dictionary<string, object> (recursive)
        private static Dictionary<string, object> ConvertJsonObjectToDict(JsonObject obj)
        {
            var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in obj)
            {
                var key = kv.Key;
                var val = kv.Value;
                if (val is JsonObject nestedObj)
                {
                    dict[key] = ConvertJsonObjectToDict(nestedObj);
                }
                else if (val is JsonArray arr)
                {
                    var list = new List<object>();
                    foreach (var it in arr)
                    {
                        if (it is JsonObject iobj) list.Add(ConvertJsonObjectToDict(iobj));
                        else if (it is JsonArray iarr) list.Add(ConvertJsonNodeToClr(iarr, VariableType.CreateScalar(VariableKind.String)));
                        else if (it is JsonValue iv)
                        {
                            try { list.Add(iv.GetValue<object>()); } catch { list.Add(iv.ToJsonString()); }
                        }
                        else list.Add(it?.ToJsonString());
                    }
                    dict[key] = list;
                }
                else if (val is JsonValue jv)
                {
                    try { dict[key] = jv.GetValue<object>(); } catch { dict[key] = jv.ToJsonString(); }
                }
                else
                {
                    dict[key] = val?.ToJsonString();
                }
            }
            return dict;
        }

        // Convert scalar input to target CLR type
        private static object ConvertScalarToTarget(object value, VariableType targetType)
        {
            if (targetType == null) return value;
            if (value == null) return GetDefaultValue(targetType);
            try
            {
                switch (targetType.Kind)
                {
                    case VariableKind.Bool:
                        if (value is bool bb) return bb;
                        if (value is string ss)
                        {
                            if (bool.TryParse(ss, out var rb)) return rb;
                            if (long.TryParse(ss, out var ln)) return ln !=0;
                        }
                        if (value is long ln2) return ln2 !=0;
                        if (value is double db2) return Math.Abs(db2) > double.Epsilon;
                        return Convert.ToBoolean(value);
                    case VariableKind.Int:
                        if (value is long l) return l;
                        if (value is int i) return (long)i;
                        if (value is double d) return Convert.ToInt64(d);
                        if (value is string sss && long.TryParse(sss, out var rl)) return rl;
                        return Convert.ToInt64(value);
                    case VariableKind.Float:
                        if (value is double dd) return dd;
                        if (value is float f) return (double)f;
                        if (value is string sf && double.TryParse(sf, NumberStyles.Any, CultureInfo.InvariantCulture, out var rf)) return rf;
                        return Convert.ToDouble(value);
                    case VariableKind.String:
                        return value.ToString();
                    case VariableKind.DateTime:
                        if (value is DateTime dt) return dt;
                        if (value is string sdt && DateTime.TryParse(sdt, out var rd)) return rd;
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

        // Serialize CLR value to JSON text for embedding into templates
        public static string ConvertToJsonString(object value, VariableType type, bool isArray = false)
        {
            if (type == null) type = VariableType.CreateScalar(VariableKind.String);
            if (value == null) return isArray ? "[]" : "null";
            if (isArray || (type != null && type.IsArray))
            {
                var elemType = type.IsArray ? type.ElementType ?? VariableType.CreateScalar(VariableKind.String) : type;
                if (value is IEnumerable e)
                {
                    var items = new List<string>();
                    foreach (var it in e) items.Add(ConvertToJsonString(it, elemType, false));
                    return "[" + string.Join(",", items) + "]";
                }
                return "[" + ConvertToJsonString(value, elemType, false) + "]";
            }

            switch (type.Kind)
            {
                case VariableKind.Bool: return value.ToString().ToLower();
                case VariableKind.Int: return Convert.ToInt64(value).ToString(CultureInfo.InvariantCulture);
                case VariableKind.Float: return Convert.ToDouble(value).ToString(CultureInfo.InvariantCulture);
                case VariableKind.String:
                    {
                        var s = value.ToString().Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
                        return "\"" + s + "\"";
                    }
                case VariableKind.DateTime:
                    return "\"" + value.ToString() + "\"";
                default:
                    return "\"" + value.ToString() + "\"";
            }
        }

        public static object GetDefaultValue(VariableType type)
        {
            if (type == null) return null;
            if (type.IsArray) return new List<object>();
            switch (type.Kind)
            {
                case VariableKind.Bool: return false;
                case VariableKind.Int: return 0L;
                case VariableKind.Float: return 0.0;
                case VariableKind.String: return string.Empty;
                case VariableKind.DateTime: return DateTime.Now;
                default: return null;
            }
        }
    }
}
