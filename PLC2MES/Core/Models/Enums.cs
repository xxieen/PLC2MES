namespace PLC2MES.Core.Models
{
    /// <summary>
    /// 基本变量种类（不含数组封装）
    /// </summary>
    public enum VariableKind
    {
        Bool,
        Int,
        Float,
        String,
        DateTime,
        Array // marker for array wrapper when used as VariableType.Kind
    }

    /// <summary>
    ///变量类型描述。用于替代原来的枚举，支持数组嵌套（ElementType）
    /// 使用方式：
    /// - 标量： new VariableType(VariableKind.String)
    /// - 数组： VariableType.CreateArray(new VariableType(VariableKind.Int))
    /// </summary>
    public class VariableType
    {
        public VariableKind Kind { get; }
        public VariableType ElementType { get; }

        public bool IsArray => Kind == VariableKind.Array;

        public VariableType(VariableKind kind, VariableType elementType = null)
        {
            Kind = kind;
            ElementType = elementType;
        }

        public static VariableType CreateScalar(VariableKind kind) => new VariableType(kind, null);
        public static VariableType CreateArray(VariableType elemType) => new VariableType(VariableKind.Array, elemType);

        public override string ToString()
        {
            if (IsArray) return $"Array<{ElementType}>";
            return Kind.ToString();
        }
    }

    /// <summary>
    ///变量来源
    /// </summary>
    public enum VariableSource
    {
        Request,
        Response
    }

    /// <summary>
    /// 表达式位置
    /// </summary>
    public enum ExpressionLocation
    {
        Url,
        Header,
        Body
    }

    /// <summary>
    ///逻辑运算符
    /// </summary>
    public enum LogicalOperator
    {
        And,
        Or
    }

    /// <summary>
    /// 比较运算符
    /// </summary>
    public enum ComparisonOperator
    {
        Equal,
        NotEqual,
        GreaterThan,
        LessThan,
        GreaterOrEqual,
        LessOrEqual,
        Like
    }
}
