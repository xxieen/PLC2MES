using System;
using System.Collections.Generic;
using System.Collections;
using System.Text.Json.Nodes;
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
        public VariableAccessor Accessor { get; set; }
        public ComparisonOperator Operator { get; set; }
        public object CompareValue { get; set; }

        public override bool Evaluate(Dictionary<string, Variable> variables)
        {
            // 通过访问器（基础变量 + .Count / [index]）先取到真正需要比较的值
            if (!VariableAccessorResolver.TryResolve(Accessor, variables, out var value, out var resolvedType))
                return false;

            try
            {
                switch (Operator)
                {
                    case ComparisonOperator.Equal:
                        return CompareEqual(value, CompareValue, resolvedType);
                    case ComparisonOperator.GreaterThan:
                        return CompareGreaterThan(value, CompareValue, resolvedType);
                    case ComparisonOperator.LessThan:
                        return CompareLessThan(value, CompareValue, resolvedType);
                    case ComparisonOperator.GreaterOrEqual:
                        return CompareGreaterThan(value, CompareValue, resolvedType) || CompareEqual(value, CompareValue, resolvedType);
                    case ComparisonOperator.LessOrEqual:
                        return CompareLessThan(value, CompareValue, resolvedType) || CompareEqual(value, CompareValue, resolvedType);
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

        public override string ToString() => $"{Accessor} {Operator} {CompareValue}";
    }

    public class BooleanVariableNode : ConditionNode
    {
        public VariableAccessor Accessor { get; set; }

        public override bool Evaluate(Dictionary<string, Variable> variables)
        {
            // 布尔节点也复用了访问器，只有最终值是 bool 时才视为 true/false
            if (!VariableAccessorResolver.TryResolve(Accessor, variables, out var value, out var resolvedType))
                return false;

            if (resolvedType != null && resolvedType.Kind == VariableKind.Bool)
                return Convert.ToBoolean(value);
            return false;
        }

        public override string ToString() => Accessor?.ToString() ?? string.Empty;
    }

    // 表示一个变量访问表达式，例如 Orders[0].Count
    public class VariableAccessor
    {
        public string BaseName { get; set; }
        // 访问步骤列表，可包含 .Count 或 [index]
        public List<AccessorSegment> Segments { get; } = new List<AccessorSegment>();

        public override string ToString()
        {
            if (Segments.Count == 0) return BaseName;
            var parts = new List<string> { BaseName };
            foreach (var segment in Segments)
            {
                if (segment.Kind == AccessorSegmentKind.Property)
                    parts.Add($".{segment.PropertyName}");
                else if (segment.Kind == AccessorSegmentKind.Index)
                    parts.Add($"[{segment.Index}]");
            }
            return string.Join(string.Empty, parts);
        }
    }

    // 单个访问步骤，Kind 决定解释方式
    public class AccessorSegment
    {
        public AccessorSegmentKind Kind { get; set; }
        public string PropertyName { get; set; }
        public int? Index { get; set; }
    }

    public enum AccessorSegmentKind
    {
        Property,
        Index
    }

    // 专门用来执行访问器的帮助类，避免在节点里堆太多解析逻辑
    internal static class VariableAccessorResolver
    {
        public static bool TryResolve(VariableAccessor accessor, Dictionary<string, Variable> variables, out object value, out VariableType resolvedType)
        {
            value = null;
            resolvedType = null;
            if (accessor == null || string.IsNullOrWhiteSpace(accessor.BaseName)) return false;
            if (!variables.TryGetValue(accessor.BaseName, out var variable) || variable == null) return false;

            value = variable.Value;
            resolvedType = variable.Type;

            // 顺序执行每一个访问步骤，任何一步失败都立即返回 false
            foreach (var segment in accessor.Segments)
            {
                switch (segment.Kind)
                {
                    case AccessorSegmentKind.Property:
                        // 目前仅支持 Count 属性
                        if (!ApplyProperty(segment.PropertyName, ref value, ref resolvedType)) return false;
                        break;
                    case AccessorSegmentKind.Index:
                        if (!segment.Index.HasValue) return false;
                        // [index] 用于按下标访问数组/List
                        if (!ApplyIndex(segment.Index.Value, ref value, ref resolvedType)) return false;
                        break;
                    default:
                        return false;
                }
            }

            return value != null;
        }

        private static bool ApplyProperty(string propertyName, ref object currentValue, ref VariableType currentType)
        {
            if (string.IsNullOrEmpty(propertyName)) return false;
            if (!propertyName.Equals("count", StringComparison.OrdinalIgnoreCase)) return false;
            if (currentValue == null) return false;

            // 针对不同的集合类型，用最直接的方式计算长度
            if (currentValue is JsonArray jsonArray)
            {
                currentValue = jsonArray.Count;
            }
            else if (currentValue is ICollection coll)
            {
                currentValue = coll.Count;
            }
            else if (currentValue is IEnumerable enumerable)
            {
                int count = 0;
                foreach (var _ in enumerable) count++; // 只能遍历统计，但确保对所有 IEnumerable 生效
                currentValue = count;
            }
            else
            {
                return false;
            }

            // Count 的结果一定是整数
            currentType = VariableType.CreateScalar(VariableKind.Int);
            return true;
        }

        private static bool ApplyIndex(int index, ref object currentValue, ref VariableType currentType)
        {
            if (index < 0 || currentValue == null) return false;

            object resolved = null;
            bool found = false;

            switch (currentValue)
            {
                case JsonArray jsonArray:
                    if (index >= jsonArray.Count) return false;
                    resolved = jsonArray[index];
                    found = true;
                    break;
                case IList list:
                    if (index >= list.Count) return false;
                    resolved = list[index];
                    found = true;
                    break;
                default:
                    if (currentValue.GetType().IsArray)
                    {
                        var arr = (Array)currentValue;
                        if (index >= arr.Length) return false;
                        resolved = arr.GetValue(index);
                        found = true;
                    }
                    else if (currentValue is IEnumerable enumerable)
                    {
                        // 对无法直接索引的 IEnumerable 逐项遍历直到命中指定下标
                        int i = 0;
                        foreach (var item in enumerable)
                        {
                            if (i == index)
                            {
                                resolved = item;
                                found = true;
                                break;
                            }
                            i++;
                        }
                    }
                    break;
            }

            if (!found) return false;

            currentValue = resolved;
            if (currentType != null)
                currentType = currentType.IsArray ? currentType.ElementType : currentType;

            return true;
        }
    }
}
