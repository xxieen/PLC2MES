using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Linq;
using PLC2MES.Core.Models;
using PLC2MES.Core.Services;
using PLC2MES.Utils;

namespace PLC2MES.Core.Processors
{
    // Added: Builds HTTP requests while honoring the structured body metadata captured during parsing.
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
                    string processedBody = ProcessBody(template, variables);
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

        // Added: Build the request body by operating directly on JsonNode so arrays can expand dynamically.
        private string ProcessBody(HttpRequestTemplate template, Dictionary<string, Variable> variables)
        {
            if (string.IsNullOrWhiteSpace(template.BodyTemplate)) return string.Empty;

            bool hasStructure = (template.ArrayTemplates?.Count ?? 0) > 0 || (template.BodyPlaceholders?.Count ?? 0) > 0;
            if (!hasStructure)
            {
                // Added: Fall back to the legacy replacement path when template未升级.
                return BuildBodyViaReplacement(template.BodyTemplate, template.Expressions, variables);
            }

            JsonNode rootNode;
            try
            {
                rootNode = _jsonProcessor.ParseJson(template.BodyTemplate);
            }
            catch (Exception ex)
            {
                Logger.LogError("HttpRequestProcessor: failed to parse body template JSON", ex);
                throw;
            }

            ApplyArrayTemplates(rootNode, template, variables);
            rootNode = ApplyScalarPlaceholders(rootNode, template.BodyPlaceholders, variables);
            return _jsonProcessor.ToJson(rootNode);
        }

        // Added: Legacy fallback when templates尚未提供结构信息。
        private string BuildBodyViaReplacement(string bodyTemplate, List<TemplateExpression> expressions, Dictionary<string, Variable> variables)
        {
            var replacements = new Dictionary<string, string>();
            var bodyExpressions = expressions.FindAll(e => e.Location == ExpressionLocation.Body);

            foreach (var expression in bodyExpressions)
            {
                if (variables.TryGetValue(expression.VariableName, out var variable))
                {
                    var targetType = variable.Type ?? expression.VariableType ?? VariableType.CreateScalar(VariableKind.String);
                    string jsonValue = TypeConverter.ConvertToJsonString(variable.Value, targetType);
                    replacements[expression.Id] = jsonValue;
                }
                else
                {
                    var dtype = expression.VariableType ?? VariableType.CreateScalar(VariableKind.String);
                    string jsonValue = TypeConverter.ConvertToJsonString(TypeConverter.GetDefaultValue(dtype), dtype);
                    replacements[expression.Id] = jsonValue;
                }
            }

            return _jsonProcessor.ReplacePlaceholders(bodyTemplate, replacements);
        }

        // Added: Iterate all recorded array descriptors and clone + populate each element.
        private void ApplyArrayTemplates(JsonNode rootNode, HttpRequestTemplate template, Dictionary<string, Variable> variables)
        {
            if (template.ArrayTemplates == null) return;

            foreach (var descriptor in template.ArrayTemplates)
            {
                var arrayNode = _jsonProcessor.GetNodeByPointer(rootNode, descriptor.CollectionPointer) as JsonArray;
                if (arrayNode == null)
                    throw new Exception($"无法定位数组节点 {descriptor.CollectionPointer}");

                var (slotRuntimes, targetLength) = BuildArrayRuntime(descriptor, variables);
                arrayNode.Clear();
                if (targetLength == 0) continue;

                bool replaceWholeElement = slotRuntimes.Count == 1 &&
                    (string.IsNullOrEmpty(slotRuntimes[0].RelativePointer) || slotRuntimes[0].RelativePointer == "/");
                if (descriptor.ElementPrototype is JsonValue) replaceWholeElement = true;

                for (int index = 0; index < targetLength; index++)
                {
                    if (replaceWholeElement)
                    {
                        var nodeValue = CreateJsonNode(slotRuntimes[0].Values[index], slotRuntimes[0].Expression.ElementType ?? slotRuntimes[0].Expression.VariableType);
                        arrayNode.Add(nodeValue);
                        continue;
                    }

                    var elementNode = descriptor.ElementPrototype?.DeepClone() ?? new JsonObject();
                    foreach (var runtime in slotRuntimes)
                    {
                        var targetType = runtime.Expression.ElementType ?? runtime.Expression.VariableType ?? VariableType.CreateScalar(VariableKind.String);
                        var valueNode = CreateJsonNode(runtime.Values[index], targetType);

                        if (string.IsNullOrEmpty(runtime.RelativePointer) || runtime.RelativePointer == "/")
                        {
                            elementNode = valueNode;
                        }
                        else
                        {
                            _jsonProcessor.SetNodeByPointer(elementNode, runtime.RelativePointer, valueNode);
                        }
                    }

                    arrayNode.Add(elementNode);
                }
            }
        }

        // Added: Materialize every slot's value sequence and determine the broadcast length.
        private (List<ArraySlotRuntime> Slots, int Length) BuildArrayRuntime(ArrayTemplateDescriptor descriptor, Dictionary<string, Variable> variables)
        {
            var runtimes = new List<ArraySlotRuntime>();
            int targetLength = 0;

            foreach (var slot in descriptor.Slots)
            {
                variables.TryGetValue(slot.Expression.VariableName, out var variable);
                var (values, isArray) = ExtractValueSequence(slot.Expression, variable);
                var runtime = new ArraySlotRuntime
                {
                    Slot = slot,
                    Values = values
                };
                runtimes.Add(runtime);

                if (isArray)
                {
                    if (targetLength == 0) targetLength = values.Count;
                    else if (targetLength != values.Count)
                        throw new Exception($"数组 {descriptor.CollectionPointer} 中变量数据长度不一致");
                }
            }

            if (targetLength == 0)
                targetLength = runtimes.Count == 0 ? 0 : runtimes.Max(r => r.Values.Count);

            if (targetLength == 0 && descriptor.Slots.Count > 0)
                targetLength = 1;

            foreach (var runtime in runtimes)
            {
                if (runtime.Values.Count == targetLength) continue;

                if (runtime.Values.Count == 0)
                {
                    var defaultValue = TypeConverter.GetDefaultValue(runtime.Expression.ElementType ?? VariableType.CreateScalar(VariableKind.String));
                    runtime.Values.Add(defaultValue);
                }

                if (runtime.Values.Count == 1 && targetLength > 1)
                {
                    var fill = runtime.Values[0];
                    while (runtime.Values.Count < targetLength)
                        runtime.Values.Add(fill);
                }
                else if (runtime.Values.Count != targetLength)
                {
                    throw new Exception($"变量 {runtime.Expression.VariableName} 的数组长度与其它变量不一致");
                }
            }

            return (runtimes, targetLength);
        }

        // Added: Convert a variable (or its default) into a list ready for per-index substitution.
        private (List<object> Values, bool IsArray) ExtractValueSequence(TemplateExpression expression, Variable variable)
        {
            var elementType = expression.ElementType ?? VariableType.CreateScalar(VariableKind.String);

            if (variable == null || variable.Value == null)
            {
                return (new List<object> { TypeConverter.GetDefaultValue(elementType) }, expression.VariableType?.IsArray ?? false);
            }

            bool treatAsArray = variable.Type?.IsArray == true || expression.VariableType?.IsArray == true;
            if (!treatAsArray && variable.Value is IEnumerable enumerable && variable.Value is not string)
                treatAsArray = true;

            if (treatAsArray)
            {
                var values = ConvertEnumerableToList(variable.Value, elementType);
                return (values, true);
            }

            var converted = TypeConverter.ConvertFromJson(variable.Value, elementType);
            return (new List<object> { converted }, false);
        }

        // Added: After arrays are handled, write remaining scalar placeholders back into the JsonNode tree.
        private JsonNode ApplyScalarPlaceholders(JsonNode rootNode, List<RequestBodyPlaceholder> placeholders, Dictionary<string, Variable> variables)
        {
            if (placeholders == null) return rootNode;

            foreach (var placeholder in placeholders)
            {
                variables.TryGetValue(placeholder.Expression.VariableName, out var variable);
                var targetType = variable?.Type ?? placeholder.Expression.VariableType ?? VariableType.CreateScalar(VariableKind.String);
                var value = variable?.Value ?? TypeConverter.GetDefaultValue(targetType);
                var node = CreateJsonNode(value, targetType);

                if (string.IsNullOrEmpty(placeholder.Pointer) || placeholder.Pointer == "/")
                {
                    rootNode = node;
                }
                else
                {
                    _jsonProcessor.SetNodeByPointer(rootNode, placeholder.Pointer, node);
                }
            }

            return rootNode;
        }

        // Added: Normalize arbitrary enumerable inputs into a strongly-typed list for broadcasting.
        private List<object> ConvertEnumerableToList(object source, VariableType elementType)
        {
            var list = new List<object>();

            if (source is JsonArray jsonArray)
            {
                foreach (var item in jsonArray)
                    list.Add(TypeConverter.ConvertFromJson(item, elementType));
                return list;
            }

            if (source is IEnumerable enumerable && source is not string)
            {
                foreach (var item in enumerable)
                    list.Add(TypeConverter.ConvertFromJson(item, elementType));
            }
            else
            {
                list.Add(TypeConverter.ConvertFromJson(source, elementType));
            }

            if (list.Count == 0)
                list.Add(TypeConverter.GetDefaultValue(elementType));

            return list;
        }

        // Added: Convert a CLR value into a JsonNode so the tree can be manipulated uniformly.
        private JsonNode CreateJsonNode(object value, VariableType targetType)
        {
            var effectiveType = targetType ?? VariableType.CreateScalar(VariableKind.String);
            string jsonValue = TypeConverter.ConvertToJsonString(value, effectiveType);
            return JsonNode.Parse(jsonValue);
        }

        // Added: Lightweight runtime container combining slot metadata and resolved values.
        private class ArraySlotRuntime
        {
            public ArrayElementSlot Slot { get; set; }
            public List<object> Values { get; set; }
            public TemplateExpression Expression => Slot.Expression;
            public string RelativePointer => Slot.RelativePointer;
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
