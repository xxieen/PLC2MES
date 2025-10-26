namespace PLC2MES.Core.Models
{
    public class ResponseMapping
    {
        public string Id { get; set; }
        public string JsonPointer { get; set; }
        public string VariableName { get; set; }
        public VariableType DataType { get; set; }
    }
}