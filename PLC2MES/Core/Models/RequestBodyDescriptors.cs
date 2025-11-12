using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace PLC2MES.Core.Models
{
    // Added: Records a single non-array placeholder pointer within the request body JSON.
    public class RequestBodyPlaceholder
    {
        // Added: Absolute JSON pointer where this placeholder lives ("/" means root).
        public string Pointer { get; set; }
        // Added: Reference back to the original template expression for type/format info.
        public TemplateExpression Expression { get; set; }
    }

    // Added: Describes an array section in the request body so we can clone and populate elements.
    public class ArrayTemplateDescriptor
    {
        // Added: Pointer to the array node (e.g. "/items").
        public string CollectionPointer { get; set; }
        // Added: Snapshot of the first element to use as a cloneable prototype.
        public JsonNode ElementPrototype { get; set; }
        // Added: Per-element slots (e.g. "/name", "/level") recorded during parsing.
        public List<ArrayElementSlot> Slots { get; } = new List<ArrayElementSlot>();
    }

    // Added: Identifies where inside an array element a placeholder lives and which expression feeds it.
    public class ArrayElementSlot
    {
        // Added: Pointer relative to the element ("/" means the entire element is replaced).
        public string RelativePointer { get; set; }
        // Added: Expression driving this slot (holds variable/type information).
        public TemplateExpression Expression { get; set; }
    }
}
