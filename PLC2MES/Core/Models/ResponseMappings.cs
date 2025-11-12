namespace PLC2MES.Core.Models
{
    // Added: Represents a header capture rule mapped to a response variable.
    public class ResponseHeaderMapping
    {
        // Added: Header name we match against (case-insensitive later on).
        public string HeaderName { get; set; }
        // Added: Regex built from the template so we can capture segments.
        public string RegexPattern { get; set; }
        // Added: Capture group index this variable should read.
        public int GroupIndex { get; set; }
        // Added: Target variable name in the variable manager.
        public string VariableName { get; set; }
        // Added: Final resolved type (array promotion already applied if needed).
        public VariableType VariableType { get; set; }
    }

    // Added: Represents a body placeholder resolved via JSON pointer or array projection.
    public class ResponseBodyMapping
    {
        // Added: Target variable info (name + final type).
        public string VariableName { get; set; }
        public VariableType VariableType { get; set; }
        // Added: Direct JSON pointer for scalar values (null when Projection is used).
        public string Pointer { get; set; }
        // Added: Array metadata if the placeholder lives under an array element.
        public ArrayProjectionInfo Projection { get; set; }

        // Added: Convenience flag so callers can branch quickly.
        public bool IsArrayProjection => Projection != null;
    }

    // Added: Stores the metadata required to aggregate array elements for a variable.
    public class ArrayProjectionInfo
    {
        // Added: Pointer to the array containing all the elements we need to iterate.
        public string CollectionPointer { get; set; }
        // Added: Pointer relative to each element ("/" means entire element).
        public string ElementPointer { get; set; }
    }
}
