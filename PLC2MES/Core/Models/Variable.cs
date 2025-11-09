using System;
using System.Collections.Generic;
using PLC2MES.Utils;

namespace PLC2MES.Core.Models
{
    public class Variable
    {
        public string Name { get; set; }
        public VariableType Type { get; set; }
        public object Value { get; set; }
        public string FormatString { get; set; }
        public VariableSource Source { get; set; }

        // User-configurable default value (used when extraction fails)
        public bool HasUserDefault { get; private set; }
        public object UserDefaultValue { get; private set; }

        public Variable() { }

        public Variable(string name, VariableType type, VariableSource source, string formatString = null)
        {
            Name = name;
            Type = type ?? throw new ArgumentNullException(nameof(type));
            Source = source;
            FormatString = formatString;
            Value = GetDefaultValue(type);
            HasUserDefault = false;
            UserDefaultValue = null;
        }

        private object GetDefaultValue(VariableType type)
        {
            if (type == null) return null;
            // arrays default to empty list
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

        public string GetFormattedValue()
        {
            if (Value == null)
                return string.Empty;

            if (Type != null && Type.IsArray)
            {
                // represent array as JSON-like string; element type used for formatting
                return TypeConverter.ConvertToJsonString(Value, Type.ElementType ?? VariableType.CreateScalar(VariableKind.String), true);
            }

            if (Type != null && Type.Kind == VariableKind.DateTime && !string.IsNullOrEmpty(FormatString))
            {
                if (Value is DateTime dt)
                    return dt.ToString(FormatString);
            }

            if (Type != null && Type.Kind == VariableKind.Bool)
            {
                return Value.ToString().ToLower();
            }

            return Value.ToString();
        }

        public bool TrySetValue(string valueString)
        {
            try
            {
                if (Type != null && Type.IsArray)
                {
                    var converted = TypeConverter.ConvertFromJson(valueString, Type.ElementType ?? VariableType.CreateScalar(VariableKind.String));
                    Value = converted;
                    return true;
                }

                var t = Type ?? VariableType.CreateScalar(VariableKind.String);
                switch (t.Kind)
                {
                    case VariableKind.Bool:
                        Value = bool.Parse(valueString);
                        return true;
                    case VariableKind.Int:
                        Value = Convert.ToInt64(valueString);
                        return true;
                    case VariableKind.Float:
                        Value = double.Parse(valueString);
                        return true;
                    case VariableKind.String:
                        Value = valueString;
                        return true;
                    case VariableKind.DateTime:
                        Value = DateTime.Parse(valueString);
                        return true;
                    default:
                        return false;
                }
            }
            catch
            {
                return false;
            }
        }

        // Set user-configured default from string. Returns true if parsed successfully.
        public bool SetUserDefaultFromString(string valueString)
        {
            try
            {
                if (Type != null && Type.IsArray)
                {
                    var converted = TypeConverter.ConvertFromJson(valueString, Type.ElementType ?? VariableType.CreateScalar(VariableKind.String));
                    HasUserDefault = true;
                    UserDefaultValue = converted;
                    return true;
                }

                object parsed = null;
                var t = Type ?? VariableType.CreateScalar(VariableKind.String);
                switch (t.Kind)
                {
                    case VariableKind.Bool:
                        parsed = bool.Parse(valueString);
                        break;
                    case VariableKind.Int:
                        parsed = Convert.ToInt64(valueString);
                        break;
                    case VariableKind.Float:
                        parsed = double.Parse(valueString);
                        break;
                    case VariableKind.String:
                        parsed = valueString;
                        break;
                    case VariableKind.DateTime:
                        parsed = DateTime.Parse(valueString);
                        break;
                    default:
                        return false;
                }
                HasUserDefault = true;
                UserDefaultValue = parsed;
                return true;
            }
            catch
            {
                return false;
            }
        }

        // Clear user-configured default
        public void ClearUserDefault()
        {
            HasUserDefault = false;
            UserDefaultValue = null;
        }

        // Get effective default (user default if set, otherwise type default)
        public object GetEffectiveDefault()
        {
            if (HasUserDefault) return UserDefaultValue;
            return GetDefaultValue(Type);
        }

        public override string ToString()
        {
            return Type == null ? $"{Name} (null) = {Value}" : $"{Name} ({Type}) = {Value}";
        }
    }
}