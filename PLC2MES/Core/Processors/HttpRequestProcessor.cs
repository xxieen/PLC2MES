using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using PLC2MES.Core.Models;
using PLC2MES.Utils;

namespace PLC2MES.Core.Processors
{
    public class HttpRequestProcessor
    {
        private JsonProcessor _jsonProcessor;
        public HttpRequestProcessor() { _jsonProcessor = new JsonProcessor(); }

        public string BuildRequest(HttpRequestTemplate template, Dictionary<string, Variable> variables)
        {
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
            return sb.ToString();
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
            try
            {
                string fullUrl = baseUrl.TrimEnd('/') + path;
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(fullUrl);
                request.Method = method;
                request.Timeout = 30000;
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
                    using (var rs = await request.GetRequestStreamAsync()) { await rs.WriteAsync(bodyBytes, 0, bodyBytes.Length); }
                }
                using (var webResp = (HttpWebResponse)await request.GetResponseAsync())
                {
                    response.StatusCode = (int)webResp.StatusCode;
                    response.StatusMessage = webResp.StatusDescription;
                    response.IsSuccess = true;
                    foreach (string key in webResp.Headers.AllKeys) response.Headers[key] = webResp.Headers[key];
                    using (var reader = new StreamReader(webResp.GetResponseStream(), Encoding.UTF8)) response.Body = await reader.ReadToEndAsync();
                }
            }
            catch (WebException ex)
            {
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
            catch (Exception ex) { response.IsSuccess = false; response.ErrorMessage = ex.Message; }
            return response;
        }
    }
}
