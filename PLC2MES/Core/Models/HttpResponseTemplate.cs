using System.Collections.Generic;

namespace PLC2MES.Core.Models
{
    // Added: Response template now distinguishes header mappings from body mappings for clarity.
    public class HttpResponseTemplate
    {
        public int? ExpectedStatusCode { get; set; }
        public Dictionary<string, string> Headers { get; set; }
        public string BodyTemplate { get; set; }
        // Added: Header mappings are tracked separately to keep the model lean.
        public List<ResponseHeaderMapping> HeaderMappings { get; }
        // Added: Body mappings describe either direct pointers or array projections.
        public List<ResponseBodyMapping> BodyMappings { get; }
        public string OriginalText { get; set; }

        public HttpResponseTemplate()  
        {
            Headers = new Dictionary<string, string>();
            HeaderMappings = new List<ResponseHeaderMapping>();
            BodyMappings = new List<ResponseBodyMapping>();
        }
    }
}
