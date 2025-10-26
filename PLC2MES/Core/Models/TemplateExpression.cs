namespace PLC2MES.Core.Models
{
    public class TemplateExpression
    {
        public string Id { get; set; }
        public string VariableName { get; set; }
        public VariableType? DataType { get; set; }
        public string FormatString { get; set; }
        public string OriginalText { get; set; }
        public ExpressionLocation Location { get; set; }
    }
}