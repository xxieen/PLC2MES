using System.Collections.Generic;

namespace PLC2MES.Core.Models
{
    /// <summary>
    /// 抽象语法树的基础节点，描述成功判定表达式的结构。
    /// 注意：节点里没有执行逻辑，运行时交给 ConditionEvaluator。
    /// </summary>
    public abstract class ConditionNode { }

    /// <summary>
    /// 表示 "左节点 &&||| 右节点"。
    /// </summary>
    public class LogicalOperatorNode : ConditionNode
    {
        public LogicalOperator Operator { get; set; }
        public ConditionNode Left { get; set; }
        public ConditionNode Right { get; set; }
        public override string ToString() => $"({Left} {Operator} {Right})";
    }

    /// <summary>
    /// 表示 "变量 与 常量 做比较" 的节点。
    /// </summary>
    public class ComparisonNode : ConditionNode
    {
        public VariableExpression LeftExpression { get; set; }
        public ComparisonOperator Operator { get; set; }
        public object CompareValue { get; set; }
        public override string ToString() => $"{LeftExpression} {Operator} {CompareValue}";
    }

    /// <summary>
    /// 表示单个布尔变量（或访问器最终指向的布尔值）。
    /// </summary>
    public class BooleanVariableNode : ConditionNode
    {
        public VariableExpression Expression { get; set; }
        public override string ToString() => Expression?.ToString() ?? string.Empty;
    }

    /// <summary>
    /// 描述变量访问链：基础变量名 + 多个属性/索引访问段。
    /// 例：Preferences[0].Count
    /// </summary>
    public class VariableExpression
    {
        public string BaseName { get; set; }
        public List<AccessorSegment> Segments { get; } = new List<AccessorSegment>();

        public override string ToString()
        {
            if (Segments.Count == 0) return BaseName;
            var parts = new List<string> { BaseName };
            foreach (var segment in Segments)
            {
                switch (segment.Kind)
                {
                    case AccessorSegmentKind.Property:
                        parts.Add($".{segment.PropertyName}");
                        break;
                    case AccessorSegmentKind.Index:
                        parts.Add($"[{segment.Index}]");
                        break;
                }
            }
            return string.Join(string.Empty, parts);
        }
    }

    /// <summary>
    /// 访问器片段：要么是属性，要么是数组/List 的索引。
    /// </summary>
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
}
