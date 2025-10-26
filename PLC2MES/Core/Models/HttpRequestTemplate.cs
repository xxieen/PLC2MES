using System.Collections.Generic;

namespace PLC2MES.Core.Models
{
    public class HttpRequestTemplate
    {
        public string Method { get; set; }
        public string Url { get; set; }
        public Dictionary<string, string> Headers { get; set; }
        public string BodyTemplate { get; set; }
        public List<TemplateExpression> Expressions { get; set; }
        public string OriginalText { get; set; }

        public HttpRequestTemplate()
        {
            Headers = new Dictionary<string, string>();
            Expressions = new List<TemplateExpression>();
        }
    }
}