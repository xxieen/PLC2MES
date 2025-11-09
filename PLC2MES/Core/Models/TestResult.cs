using System;
using System.Collections.Generic;

namespace PLC2MES.Core.Models
{
    public class TestResult
    {
        public bool Success { get; set; }
        public int StatusCode { get; set; }
        public string RequestText { get; set; }
        public string ResponseText { get; set; } 
        public Dictionary<string, Variable> ExtractedVariables { get; set; }
        public bool? SuccessCriteriaResult { get; set; }
        public string SuccessCriteriaDetail { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime ExecutionTime { get; set; }
        public long DurationMs { get; set; }

        public TestResult()
        {
            ExtractedVariables = new Dictionary<string, Variable>();
            ExecutionTime = DateTime.Now;
        }
    }
}
