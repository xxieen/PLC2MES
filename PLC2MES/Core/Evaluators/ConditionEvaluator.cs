using System;
using System.Collections.Generic;
using PLC2MES.Core.Models;

namespace PLC2MES.Core.Evaluators
{
    /// <summary>
    /// 解释 ConditionNode 抽象语法树。
    /// 负责递归遍历节点 + 调用 VariableExpressionEvaluator 把变量值拿出来，再做比较。
    /// </summary>
    public class ConditionEvaluator
    {
        private readonly VariableExpressionEvaluator _expressionEvaluator = new VariableExpressionEvaluator();

        public bool Evaluate(ConditionNode node, IDictionary<string, Variable> variables, out string failureReason)
        {
            failureReason = null;
            if (node == null)
            {
                failureReason = "表达式为空";
                return false;
            }

            switch (node)
            {
                case LogicalOperatorNode logical:
                    return EvaluateLogical(logical, variables, out failureReason);
                case NotNode notNode:
                    return EvaluateNot(notNode, variables, out failureReason);
                case ComparisonNode comparison:
                    return EvaluateComparison(comparison, variables, out failureReason);
                case BooleanVariableNode booleanNode:
                    return EvaluateBoolean(booleanNode, variables, out failureReason);
                default:
                    failureReason = $"未知的节点类型 {node.GetType().Name}";
                    return false;
            }
        }

        private bool EvaluateLogical(LogicalOperatorNode node, IDictionary<string, Variable> variables, out string failureReason)
        {
            failureReason = null;

            if (!Evaluate(node.Left, variables, out var leftFailure))
            {
                if (node.Operator == LogicalOperator.And)
                {
                    failureReason = leftFailure;
                    return false;
                }
            }
            else if (node.Operator == LogicalOperator.Or)
            {
                return true;
            }

            bool right = Evaluate(node.Right, variables, out var rightFailure);
            if (!right)
            {
                failureReason = rightFailure;
            }
            return right;
        }

        private bool EvaluateComparison(ComparisonNode node, IDictionary<string, Variable> variables, out string failureReason)
        {
            failureReason = null;
            if (!_expressionEvaluator.TryEvaluate(node.LeftExpression, variables, out var value, out var resolvedType, out var error))
            {
                failureReason = error;
                return false;
            }

            try
            {
                switch (node.Operator)
                {
                    case ComparisonOperator.Equal:
                        return CompareEqual(value, node.CompareValue, resolvedType);
                    case ComparisonOperator.NotEqual:
                        return !CompareEqual(value, node.CompareValue, resolvedType);
                    case ComparisonOperator.GreaterThan:
                        return CompareGreaterThan(value, node.CompareValue, resolvedType);
                    case ComparisonOperator.LessThan:
                        return CompareLessThan(value, node.CompareValue, resolvedType);
                    case ComparisonOperator.GreaterOrEqual:
                        return CompareGreaterThan(value, node.CompareValue, resolvedType) ||
                               CompareEqual(value, node.CompareValue, resolvedType);
                    case ComparisonOperator.LessOrEqual:
                        return CompareLessThan(value, node.CompareValue, resolvedType) ||
                               CompareEqual(value, node.CompareValue, resolvedType);
                    case ComparisonOperator.Like:
                        return CompareLike(value, node.CompareValue);
                    default:
                        failureReason = $"未知的比较符 {node.Operator}";
                        return false;
                }
            }
            catch (Exception ex)
            {
                failureReason = $"比较失败: {ex.Message}";
                return false;
            }
        }

        private bool EvaluateBoolean(BooleanVariableNode node, IDictionary<string, Variable> variables, out string failureReason)
        {
            failureReason = null;
            if (!_expressionEvaluator.TryEvaluate(node.Expression, variables, out var value, out var resolvedType, out var error))
            {
                failureReason = error;
                return false;
            }

            if (resolvedType != null && resolvedType.Kind == VariableKind.Bool)
            {
                return Convert.ToBoolean(value);
            }

            failureReason = $"表达式 {node.Expression} 不是布尔类型";
            return false;
        }

        #region 比较帮助方法

        private bool EvaluateNot(NotNode node, IDictionary<string, Variable> variables, out string failureReason)
        {
            failureReason = null;
            var inner = Evaluate(node.Inner, variables, out var innerFailure);
            if (!inner && !string.IsNullOrEmpty(innerFailure))
            {
                // 如果子节点失败（变量缺失等），直接把原因向上冒泡，不进行取反
                failureReason = innerFailure;
                return false;
            }
            return !inner;
        }

        private bool CompareEqual(object value, object compareValue, VariableType type)
        {
            if (type == null) return value?.Equals(compareValue) ?? compareValue == null;

            switch (type.Kind)
            {
                case VariableKind.Bool:
                    return Convert.ToBoolean(value) == Convert.ToBoolean(compareValue);
                case VariableKind.Int:
                    return Convert.ToInt64(value) == Convert.ToInt64(compareValue);
                case VariableKind.Float:
                    return Math.Abs(Convert.ToDouble(value) - Convert.ToDouble(compareValue)) < 0.000001;
                case VariableKind.String:
                    return string.Equals(Convert.ToString(value), Convert.ToString(compareValue), StringComparison.Ordinal);
                case VariableKind.DateTime:
                    return Convert.ToDateTime(value) == Convert.ToDateTime(compareValue);
                default:
                    return Equals(value, compareValue);
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
                    return string.Compare(Convert.ToString(value), Convert.ToString(compareValue), StringComparison.Ordinal) > 0;
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
                    return string.Compare(Convert.ToString(value), Convert.ToString(compareValue), StringComparison.Ordinal) < 0;
                default:
                    return false;
            }
        }

        private bool CompareLike(object value, object compareValue)
        {
            var str = Convert.ToString(value) ?? string.Empty;
            var pattern = Convert.ToString(compareValue) ?? string.Empty;

            if (pattern.StartsWith("%") && pattern.EndsWith("%"))
            {
                var inner = pattern[1..^1];
                return str.Contains(inner, StringComparison.Ordinal);
            }
            if (pattern.EndsWith("%"))
            {
                var prefix = pattern[..^1];
                return str.StartsWith(prefix, StringComparison.Ordinal);
            }
            if (pattern.StartsWith("%"))
            {
                var suffix = pattern[1..];
                return str.EndsWith(suffix, StringComparison.Ordinal);
            }

            return string.Equals(str, pattern, StringComparison.Ordinal);
        }

        #endregion
    }
}
