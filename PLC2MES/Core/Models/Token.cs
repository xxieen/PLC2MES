namespace PLC2MES.Core.Models
{
    public enum TokenType
    {
        Variable,
        Operator,
        LogicalOperator,
        Value,
        LeftParen,
        RightParen,
        LeftBracket,
        RightBracket,
        Dot,
        End
    }

    public class Token
    {
        public TokenType Type { get; set; }
        public string Value { get; set; }
        public Token(TokenType type, string value) { Type = type; Value = value; }
        public override string ToString() => $"{Type}: {Value}";
    }
} 
