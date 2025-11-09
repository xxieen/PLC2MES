using System;
using System.Collections.Generic;
using PLC2MES.Core.Models;

namespace PLC2MES.Core.Models
{
    public abstract class ConditionNode
    {
        public abstract bool Evaluate(Dictionary<string, Variable> variables);
    }

    public class LogicalOperatorNode : ConditionNode
    {
        public LogicalOperator Operator { get; set; }
        public ConditionNode Left { get; set; }
        public ConditionNode Right { get; set; }

        public override bool Evaluate(Dictionary<string, Variable> variables)
        {
            bool leftResult = Left.Evaluate(variables);
            if (Operator == LogicalOperator.And && !leftResult) return false;
            if (Operator == LogicalOperator.Or && leftResult) return true;
            bool rightResult = Right.Evaluate(variables);
            return Operator == LogicalOperator.And ? leftResult && rightResult : leftResult || rightResult;
        }

        public override string ToString() => $"({Left} {Operator} {Right})";
    }

    public class ComparisonNode : ConditionNode
    {
        public string VariableName { get; set; }
        public ComparisonOperator Operator { get; set; }
        public object CompareValue { get; set; }

        public override bool Evaluate(Dictionary<string, Variable> variables)
        {
            // Special variable $StatusCode may be provided in variables
            if (!variables.ContainsKey(VariableName)) return false;
            var variable = variables[VariableName];
            var value = variable.Value;
            if (value == null) return false;

            try
            {
                switch (Operator)
                {
                    case ComparisonOperator.Equal:
                        return CompareEqual(value, CompareValue, variable.Type);
                    case ComparisonOperator.GreaterThan:
                        return CompareGreaterThan(value, CompareValue, variable.Type);
                    case ComparisonOperator.LessThan:
                        return CompareLessThan(value, CompareValue, variable.Type);
                    case ComparisonOperator.GreaterOrEqual:
                        return CompareGreaterThan(value, CompareValue, variable.Type) || CompareEqual(value, CompareValue, variable.Type);
                    case ComparisonOperator.LessOrEqual:
                        return CompareLessThan(value, CompareValue, variable.Type) || CompareEqual(value, CompareValue, variable.Type);
                    case ComparisonOperator.Like:
                        return CompareLike(value, CompareValue);
                    default:
                        return false;
                }
            }
            catch
            {
                return false;
            }
        }

        private bool CompareEqual(object value, object compareValue, VariableType type)
        {
            if (type == null) return false;
            switch (type.Kind)
            {
                case VariableKind.Bool:
                    return Convert.ToBoolean(value) == Convert.ToBoolean(compareValue);
                case VariableKind.Int:
                    return Convert.ToInt64(value) == Convert.ToInt64(compareValue);
                case VariableKind.Float:
                    return Math.Abs(Convert.ToDouble(value) - Convert.ToDouble(compareValue)) <0.000001;
                case VariableKind.String:
                    return value.ToString() == compareValue.ToString();
                case VariableKind.DateTime:
                    return Convert.ToDateTime(value) == Convert.ToDateTime(compareValue);
                default:
                    return false;
            }
        }

        private bool CompareGreaterThan(object value, object compareValue, VariableType type)
        {
            if (type == null) return false;
            switch (type.Kind)
            {
                case VariableKind.Int:
                    return Convert.ToInt64(value) > Convert.ToInt64(compareValue);
                case VariableKind.Float:
                    return Convert.ToDouble(value) > Convert.ToDouble(compareValue);
                case VariableKind.DateTime:
                    return Convert.ToDateTime(value) > Convert.ToDateTime(compareValue);
                case VariableKind.String:
                    return string.Compare(value.ToString(), compareValue.ToString(), StringComparison.Ordinal) >0;
                default:
                    return false;
            }
        }

        private bool CompareLessThan(object value, object compareValue, VariableType type)
        {
            if (type == null) return false;
            switch (type.Kind)
            {
                case VariableKind.Int:
                    return Convert.ToInt64(value) < Convert.ToInt64(compareValue);
                case VariableKind.Float:
                    return Convert.ToDouble(value) < Convert.ToDouble(compareValue);
                case VariableKind.DateTime:
                    return Convert.ToDateTime(value) < Convert.ToDateTime(compareValue);
                case VariableKind.String:
                    return string.Compare(value.ToString(), compareValue.ToString(), StringComparison.Ordinal) <0;
                default:
                    return false;
            }
        }

        private bool CompareLike(object value, object compareValue)
        {
            string str = value.ToString();
            string pattern = compareValue.ToString();
            if (pattern.StartsWith("%") && pattern.EndsWith("%"))
            {
                var inner = pattern.Substring(1, pattern.Length -2);
                return str.Contains(inner);
            }
            if (pattern.EndsWith("%"))
            {
                var prefix = pattern.Substring(0, pattern.Length -1);
                return str.StartsWith(prefix);
            }
            if (pattern.StartsWith("%"))
            {
                var suffix = pattern.Substring(1);
                return str.EndsWith(suffix);
            }
            return str == pattern;
        }

        public override string ToString() => $"{VariableName} {Operator} {CompareValue}";
    }

    public class BooleanVariableNode : ConditionNode
    {
        public string VariableName { get; set; }

        public override bool Evaluate(Dictionary<string, Variable> variables)
        {
            if (!variables.ContainsKey(VariableName)) return false;
            var variable = variables[VariableName];
            if (variable.Type != null && variable.Type.Kind == VariableKind.Bool)
                return Convert.ToBoolean(variable.Value);
            return false;
        }

        public override string ToString() => VariableName;
    }
}
