using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using PLC2MES.Core.Models;
using PLC2MES.Core.Parsers;
using PLC2MES.Core.Processors;

namespace PLC2MES.Core.Services
{
    public class HttpTestService
    {
        private RequestTemplateParser _requestParser;
        private ResponseTemplateParser _response_parser;
        private SuccessCriteriaParser _criteria_parser;
        private HttpRequestProcessor _requestProcessor;
        private HttpResponseProcessor _responseProcessor;
        private IVariableManager _variableManager;
        private HttpRequestTemplate _requestTemplate;
        private HttpResponseTemplate _response_template;
        private ConditionNode _successCriteria;
        private string _baseUrl;

        public HttpTestService(IVariableManager manager)
        {
            _variableManager = manager ?? throw new ArgumentNullException(nameof(manager));
            _requestParser = new RequestTemplateParser(_variableManager);
            _response_parser = new ResponseTemplateParser(_variableManager);
            _criteria_parser = new SuccessCriteriaParser();
            _requestProcessor = new HttpRequestProcessor(_variableManager);
            _responseProcessor = new HttpResponseProcessor(_variableManager);
        }

        public void SetBaseUrl(string baseUrl) { _baseUrl = baseUrl; }

        public void LoadRequestTemplate(string templateText)
        {
            _requestTemplate = _requestParser.Parse(templateText);
        }

        public void LoadResponseTemplate(string templateText)
        {
            _response_template = _response_parser.Parse(templateText);
        }

        public void LoadSuccessCriteria(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression)) { _successCriteria = null; return; }
            _successCriteria = _criteria_parser.Parse(expression);
        }

        public IVariableManager GetVariableManager() => _variableManager;

        public ValidationResult ValidateBeforeExecution()
        {
            var r = new ValidationResult { IsValid = true };
            if (_requestTemplate == null) { r.IsValid = false; r.ErrorMessages.Add("未加载请求模板"); }
            if (_response_template == null) { r.IsValid = false; r.ErrorMessages.Add("未加载响应模板"); }
            if (string.IsNullOrWhiteSpace(_baseUrl)) { r.IsValid = false; r.ErrorMessages.Add("未设置基础URL"); }
            if (!_variableManager.AreAllRequestVariablesSet()) { r.IsValid = false; r.ErrorMessages.Add("存在未赋值的请求变量"); }
            return r;
        }

        public async Task<TestResult> ExecuteTestAsync()
        {
            var result = new TestResult();
            var sw = Stopwatch.StartNew();
            try
            {
                var valid = ValidateBeforeExecution();
                if (!valid.IsValid) { result.Success = false; result.ErrorMessage = valid.GetErrorMessage(); return result; }
                string requestText = _requestProcessor.BuildRequest(_requestTemplate);
                result.RequestText = requestText;
                var response = await _requestProcessor.SendRequestAsync(_baseUrl, _requestTemplate.Method, ExtractPath(_requestTemplate.Url), _requestTemplate.Headers, ExtractBody(requestText));
                result.StatusCode = response.StatusCode;
                result.ResponseText = _responseProcessor.FormatResponse(response);
                if (!response.IsSuccess) { result.Success = false; result.ErrorMessage = response.ErrorMessage; sw.Stop(); result.DurationMs = sw.ElapsedMilliseconds; return result; }
                _responseProcessor.ProcessResponse(response, _response_template);
                result.ExtractedVariables = new Dictionary<string, Variable>(_variableManager.GetAllVariables());
                if (_successCriteria != null) { bool cr = _successCriteria.Evaluate(_variableManager.GetAllVariables()); result.SuccessCriteriaResult = cr; result.SuccessCriteriaDetail = BuildCriteriaDetail(cr); result.Success = cr; }
                else { result.Success = response.StatusCode >= 200 && response.StatusCode < 300; }
            }
            catch (Exception ex) { result.Success = false; result.ErrorMessage = ex.Message; }
            finally { sw.Stop(); result.DurationMs = sw.ElapsedMilliseconds; }
            return result;
        }

        private string ExtractPath(string url) { return url; }
        private string ExtractBody(string requestText) { var parts = requestText.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.None); if (parts.Length > 1) return parts[1].Trim(); return string.Empty; }

        private string BuildCriteriaDetail(bool result)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"成功条件评估结果: {(result ? "通过" : "失败")}");
            sb.AppendLine();
            sb.AppendLine("变量值:");
            foreach (var v in _variableManager.GetAllVariables().Values) sb.AppendLine($" {v.Name} = {v.Value} ({v.Type})");
            return sb.ToString();
        }

        // Reset service state
        public void Reset()
        {
            _requestTemplate = null;
            _response_template = null;
            _successCriteria = null;
            _variableManager.Clear();
        }
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> ErrorMessages { get; set; } = new List<string>();
        public string GetErrorMessage() => string.Join("\n", ErrorMessages);
    }
}
