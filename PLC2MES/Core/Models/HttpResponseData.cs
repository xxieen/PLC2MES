using System.Collections.Generic;

namespace PLC2MES.Core.Models
{
    public class HttpResponseData
    {
        public int StatusCode { get; set; }
        public string StatusMessage { get; set; }
        public Dictionary<string, List<string>> Headers { get; set; }
        public string Body { get; set; }
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }

        public HttpResponseData()
        {
            Headers = new Dictionary<string, List<string>>();
        }
    }
}
 