using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using PLC2MES.Core.Models;
using PLC2MES.Core.Services;
using PLC2MES.Utils;

namespace PLC2MES.Core.Processors
{
    public class HttpRequestProcessor
    {
        private JsonProcessor _jsonProcessor;
        private readonly IVariableManager _vars;

        public HttpRequestProcessor(IVariableManager vars)
        {
            _jsonProcessor = new JsonProcessor();
            _vars = vars ?? throw new ArgumentNullException(nameof(vars));
        }

        public string BuildRequest(HttpRequestTemplate template)
        {
            Logger.LogInfo("HttpRequestProcessor: BuildRequest called");
            try
            {
                var variables = _vars.GetAllVariables();
                var sb = new StringBuilder();
                string processedUrl = ProcessUrl(template.Url, template.Expressions, variables);
                sb.AppendLine($"{template.Method} {processedUrl}");
                foreach (var header in template.Headers)
                {
                    string processedValue = ProcessHeaderValue(header.Value, template.Expressions, variables);
                    sb.AppendLine($"{header.Key}: {processedValue}");
                }
                if (!string.IsNullOrWhiteSpace(template.BodyTemplate))
                {
                    sb.AppendLine();
                    string processedBody = ProcessBody(template.BodyTemplate, template.Expressions, variables);
                    sb.Append(processedBody);
                }
                var requestText = sb.ToString();
                Logger.LogInfo($"HttpRequestProcessor: BuildRequest finished, method={template.Method}, url={processedUrl}, bodyLength={requestText.Length}");
                return requestText;
            }
            catch (Exception ex)
            {
                Logger.LogError("HttpRequestProcessor: BuildRequest failed", ex);
                throw;
            }
        }

        private string ProcessUrl(string url, List<TemplateExpression> expressions, Dictionary<string, Variable> variables)
        {
            string result = url;
            var urlExpressions = expressions.FindAll(e => e.Location == ExpressionLocation.Url);
            foreach (var expression in urlExpressions)
            {
                if (variables.ContainsKey(expression.VariableName))
                {
                    var variable = variables[expression.VariableName];
                    string value = variable.GetFormattedValue();
                    result = result.Replace(expression.OriginalText, value);
                }
            }
            return result;
        }

        private string ProcessHeaderValue(string headerValue, List<TemplateExpression> expressions, Dictionary<string, Variable> variables)
        {
            string result = headerValue;
            var headerExpressions = expressions.FindAll(e => e.Location == ExpressionLocation.Header);
            foreach (var expression in headerExpressions)
            {
                if (headerValue.Contains(expression.OriginalText))
                {
                    if (variables.ContainsKey(expression.VariableName))
                    {
                        var variable = variables[expression.VariableName];
                        string value = variable.GetFormattedValue();
                        result = result.Replace(expression.OriginalText, value);
                    }
                }
            }
            return result;
        }

        private string ProcessBody(string bodyTemplate, List<TemplateExpression> expressions, Dictionary<string, Variable> variables)
        {
            var replacements = new Dictionary<string, string>();
            var bodyExpressions = expressions.FindAll(e => e.Location == ExpressionLocation.Body);
            foreach (var expression in bodyExpressions)
            {
                if (variables.ContainsKey(expression.VariableName))
                {
                    var variable = variables[expression.VariableName];
                    string jsonValue = TypeConverter.ConvertToJsonString(variable.Value, variable.Type);
                    replacements[expression.Id] = jsonValue;
                }
                else
                {
                    var def = TypeConverter.GetDefaultValue(expression.DataType.Value);
                    string jsonValue = TypeConverter.ConvertToJsonString(def, expression.DataType.Value);
                    replacements[expression.Id] = jsonValue;
                }
            }
            return _jsonProcessor.ReplacePlaceholders(bodyTemplate, replacements);
        }

        public async Task<HttpResponseData> SendRequestAsync(string baseUrl, string method, string path, Dictionary<string, string> headers, string body)
        {
            var response = new HttpResponseData();
            Logger.LogInfo($"HttpRequestProcessor: SendRequestAsync called, baseUrl={baseUrl}, method={method}, path={path}, headers={headers?.Count ??0}, bodyLength={(body?.Length ??0)}");
            try
            {
                string fullUrl = baseUrl.TrimEnd('/') + path;
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(fullUrl);
                request.Method = method;
                request.Timeout =30000;
                foreach (var header in headers)
                {
                    switch (header.Key.ToLower())
                    {
                        case "content-type": request.ContentType = header.Value; break;
                        case "user-agent": request.UserAgent = header.Value; break;
                        case "accept": request.Accept = header.Value; break;
                        default: request.Headers.Add(header.Key, header.Value); break;
                    }
                }
                if (!string.IsNullOrEmpty(body) && (method == "POST" || method == "PUT" || method == "PATCH"))
                {
                    var bodyBytes = Encoding.UTF8.GetBytes(body);
                    request.ContentLength = bodyBytes.Length;
                    using (var rs = await request.GetRequestStreamAsync()) { await rs.WriteAsync(bodyBytes,0, bodyBytes.Length); }
                }
                using (var webResp = (HttpWebResponse)await request.GetResponseAsync())
                {
                    response.StatusCode = (int)webResp.StatusCode;
                    response.StatusMessage = webResp.StatusDescription;
                    response.IsSuccess = true;
                    foreach (string key in webResp.Headers.AllKeys)
                    {
                        var vals = webResp.Headers.GetValues(key);
                        if (vals != null)
                            response.Headers[key] = new List<string>(vals);
                        else
                            response.Headers[key] = new List<string> { webResp.Headers[key] ?? string.Empty };
                    }
                    using (var reader = new StreamReader(webResp.GetResponseStream(), Encoding.UTF8)) response.Body = await reader.ReadToEndAsync();
                    Logger.LogInfo($"HttpRequestProcessor: Received response status={response.StatusCode}");
                }
            }
            catch (WebException ex)
            {
                Logger.LogError("HttpRequestProcessor: WebException during SendRequestAsync", ex);
                if (ex.Response != null)
                {
                    var err = (HttpWebResponse)ex.Response;
                    response.StatusCode = (int)err.StatusCode;
                    response.StatusMessage = err.StatusDescription;
                    response.IsSuccess = false;
                    response.ErrorMessage = ex.Message;
                    try { using (var reader = new StreamReader(err.GetResponseStream(), Encoding.UTF8)) response.Body = reader.ReadToEnd(); } catch { response.Body = string.Empty; }
                }
                else { response.IsSuccess = false; response.ErrorMessage = ex.Message; }
            }
            catch (Exception ex)
            {
                Logger.LogError("HttpRequestProcessor: Exception during SendRequestAsync", ex);
                response.IsSuccess = false;
                response.ErrorMessage = ex.Message;
            }
            return response;
        }
    }
}
