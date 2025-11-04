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

        // Whether this variable represents an array of elements of 'Type'
        public bool IsArray { get; set; }

        // User-configurable default value (used when extraction fails)
        public bool HasUserDefault { get; private set; }
        public object UserDefaultValue { get; private set; }

        public Variable() { }

        public Variable(string name, VariableType type, VariableSource source, string formatString = null)
        {
            Name = name;
            Type = type;
            Source = source;
            FormatString = formatString;
            IsArray = false;
            Value = GetDefaultValue(type);
            HasUserDefault = false;
            UserDefaultValue = null;
        }

        private object GetDefaultValue(VariableType type)
        {
            switch (type)
            {
                case VariableType.Bool:
                    return false;
                case VariableType.Int:
                    return 0;
                case VariableType.Float:
                    return 0.0;
                case VariableType.String:
                    return string.Empty;
                case VariableType.DateTime:
                    return DateTime.Now;
                default:
                    return null;
            }
        }

        public string GetFormattedValue()
        {
            if (Value == null)
                return string.Empty;

            if (IsArray)
            {
                // represent array as JSON-like string
                return TypeConverter.ConvertToJsonString(Value, Type, true);
            }

            if (Type == VariableType.DateTime && !string.IsNullOrEmpty(FormatString))
            {
                if (Value is DateTime dt)
                    return dt.ToString(FormatString);
            }

            if (Type == VariableType.Bool)
            {
                return Value.ToString().ToLower();
            }

            return Value.ToString();
        }

        public bool TrySetValue(string valueString)
        {
            try
            {
                if (IsArray)
                {
                    var converted = TypeConverter.ConvertFromJson(valueString, Type, true);
                    Value = converted;
                    return true;
                }

                switch (Type)
                {
                    case VariableType.Bool:
                        Value = bool.Parse(valueString);
                        return true;
                    case VariableType.Int:
                        Value = int.Parse(valueString);
                        return true;
                    case VariableType.Float:
                        Value = double.Parse(valueString);
                        return true;
                    case VariableType.String:
                        Value = valueString;
                        return true;
                    case VariableType.DateTime:
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
                if (IsArray)
                {
                    var converted = TypeConverter.ConvertFromJson(valueString, Type, true);
                    HasUserDefault = true;
                    UserDefaultValue = converted;
                    return true;
                }

                object parsed = null;
                switch (Type)
                {
                    case VariableType.Bool:
                        parsed = bool.Parse(valueString);
                        break;
                    case VariableType.Int:
                        parsed = int.Parse(valueString);
                        break;
                    case VariableType.Float:
                        parsed = double.Parse(valueString);
                        break;
                    case VariableType.String:
                        parsed = valueString;
                        break;
                    case VariableType.DateTime:
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
            return $"{Name} ({Type}) = {Value}";
        }
    }
}
