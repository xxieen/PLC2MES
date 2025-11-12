using System.Collections.Generic;

namespace PLC2MES.Core.Models
{
    // Added: Request template now stores extra metadata describing body placeholders and arrays.
    public class HttpRequestTemplate
    {
        public string Method { get; set; }
        public string Url { get; set; }
        public Dictionary<string, string> Headers { get; set; }
        public string BodyTemplate { get; set; } 
        public List<TemplateExpression> Expressions { get; set; }
        // Added: Track simple placeholders that can be set with JSON pointers during request build.
        public List<RequestBodyPlaceholder> BodyPlaceholders { get; }
        // Added: Track each array block so we can clone and populate elements deterministically.
        public List<ArrayTemplateDescriptor> ArrayTemplates { get; }
        public string OriginalText { get; set; }

        public HttpRequestTemplate()
        {
            Headers = new Dictionary<string, string>();
            Expressions = new List<TemplateExpression>();
            BodyPlaceholders = new List<RequestBodyPlaceholder>();
            ArrayTemplates = new List<ArrayTemplateDescriptor>();
        }
    }
}
