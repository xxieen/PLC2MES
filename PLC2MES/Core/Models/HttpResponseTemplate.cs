using System.Collections.Generic;

namespace PLC2MES.Core.Models
{
    public class HttpResponseTemplate
    {
        public int? ExpectedStatusCode { get; set; }
        public Dictionary<string, string> Headers { get; set; }
        public string BodyTemplate { get; set; }
        public List<ResponseMapping> Mappings { get; set; }
        public string OriginalText { get; set; }

        public HttpResponseTemplate()
        {
            Headers = new Dictionary<string, string>();
            Mappings = new List<ResponseMapping>();
        }
    }
}