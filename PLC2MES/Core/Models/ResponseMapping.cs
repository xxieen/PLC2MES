namespace PLC2MES.Core.Models
{
    public class ResponseMapping
    {
        public string Id { get; set; }
        public string JsonPointer { get; set; }
        public string VariableName { get; set; }
        public VariableType DataType { get; set; }
        // If mapping targets a response header, HeaderName is set (case-insensitive)
        public string HeaderName { get; set; }
        // For embedded header templates, a regex pattern with capture groups can be used
        public string HeaderRegex { get; set; }
        //1-based index of capture group within HeaderRegex that contains this mapping's value
        public int HeaderGroupIndex { get; set; }
    }
}