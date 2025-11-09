namespace PLC2MES.Core.Models
{
    public class TemplateExpression
    {
        public string Id { get; set; }
        public string VariableName { get; set; }
        // For scalar expressions DataType is the scalar type; for array expressions DataType represents the element type and the VariableType.IsArray flag is set on the Variable/TemplateExpression's VariableType instance
        public VariableType DataType { get; set; }
        public string FormatString { get; set; }
        public string OriginalText { get; set; }
        public ExpressionLocation Location { get; set; }
    }
}