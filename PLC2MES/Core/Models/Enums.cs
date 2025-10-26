namespace PLC2MES.Core.Models
{
    /// <summary>
    ///变量数据类型
    /// </summary>
    public enum VariableType
    {
        Bool,
        Int,
        Float,
        String,
        DateTime
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
        GreaterThan,
        LessThan,
        GreaterOrEqual,
        LessOrEqual,
        Like
    }
}