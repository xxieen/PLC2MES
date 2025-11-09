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

        // 当模板中的 @Array<T> 位于数组元素内部时，需要记住要遍历的数组根节点，例如 /preferences
        public string CollectionPointer { get; set; }
        // 记录数组元素内部的相对路径，例如 /category；为空或 "/" 表示直接取整个元素
        public string ElementRelativePointer { get; set; }
        
        // compatibility: whether mapping expects an array (can be inferred from DataType)
        public bool IsArray => DataType != null && DataType.IsArray;
    }
}
