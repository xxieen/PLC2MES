using System;
using System.Globalization;
using PLC2MES.Core.Models;

namespace PLC2MES.Utils
{
    public static class TypeConverter
    {
        public static object ConvertFromJson(object jsonValue, VariableType targetType)
        {
            if (jsonValue == null) return GetDefaultValue(targetType);
            try
            {
                switch (targetType)
                {
                    case VariableType.Bool: return Convert.ToBoolean(jsonValue);
                    case VariableType.Int: return Convert.ToInt64(jsonValue);
                    case VariableType.Float: return Convert.ToDouble(jsonValue);
                    case VariableType.String: return jsonValue.ToString();
                    case VariableType.DateTime: return Convert.ToDateTime(jsonValue);
                    default: return jsonValue;
                }
            }
            catch { return GetDefaultValue(targetType); }
        }

        public static string ConvertToJsonString(object value, VariableType type)
        {
            if (value == null) return "null";
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
