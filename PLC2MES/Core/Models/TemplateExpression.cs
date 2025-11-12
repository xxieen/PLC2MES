namespace PLC2MES.Core.Models
{
    // Added: Central record describing every placeholder found in request/response templates.
    public class TemplateExpression
    {
        public string Id { get; set; }
        public string VariableName { get; set; }
        // Added: VariableType now stores the final resolved type so runtime does not need to upgrade it.
        public VariableType VariableType { get; set; }
        public string FormatString { get; set; }
        public string OriginalText { get; set; }
        public ExpressionLocation Location { get; set; }

        // Added: Helper to quickly fetch the element type when VariableType represents an array.
        public VariableType ElementType => VariableType != null && VariableType.IsArray
            ? VariableType.ElementType
            : VariableType;
    }
}
