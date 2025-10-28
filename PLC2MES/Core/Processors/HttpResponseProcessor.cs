using System;
using System.Collections.Generic;
using PLC2MES.Core.Models;
using PLC2MES.Utils;

namespace PLC2MES.Core.Processors
{
    public class HttpResponseProcessor
    {
        private JsonProcessor _jsonProcessor;
        public HttpResponseProcessor() { _jsonProcessor = new JsonProcessor(); }

        public void ProcessResponse(HttpResponseData response, HttpResponseTemplate template, Dictionary<string, Variable> variables)
        {
            Logger.LogInfo($"HttpResponseProcessor: ProcessResponse called, status={response?.StatusCode}");
            if (response == null || template == null) return;
            if (!variables.ContainsKey("$StatusCode")) variables["$StatusCode"] = new Variable("$StatusCode", VariableType.Int, VariableSource.Response);
            variables["$StatusCode"].Value = response.StatusCode;
            if (string.IsNullOrWhiteSpace(response.Body))
            {
                Logger.LogInfo("HttpResponseProcessor: empty response body, nothing to extract");
                return;
            }
            try
            {
                var node = _jsonProcessor.ParseJson(response.Body);
                Logger.LogInfo($"HttpResponseProcessor: parsed JSON, mappings to process={template.Mappings.Count}");
                foreach (var mapping in template.Mappings)
                {
                    ExtractVariable(node, mapping, variables);
                }
                Logger.LogInfo("HttpResponseProcessor: ProcessResponse finished");
            }
            catch (Exception ex)
            {
                Logger.LogError("HttpResponseProcessor: failed to parse/process response JSON", ex);
                throw new Exception($"œÏ”¶JSONΩ‚Œˆ ß∞‹: {ex.Message}");
            }
        }

        private void ExtractVariable(System.Text.Json.Nodes.JsonNode jsonRoot, ResponseMapping mapping, Dictionary<string, Variable> variables)
        {
            try
            {
                var value = _jsonProcessor.GetValueByPointer(jsonRoot, mapping.JsonPointer);
                if (value == null)
                {
                    Logger.LogInfo($"HttpResponseProcessor: value not found for mapping {mapping.VariableName} at {mapping.JsonPointer}, setting default");
                    SetVariableDefaultValue(mapping.VariableName, mapping.DataType, variables);
                    return;
                }
                var converted = TypeConverter.ConvertFromJson(value, mapping.DataType);
                if (variables.ContainsKey(mapping.VariableName))
                {
                    variables[mapping.VariableName].Value = converted;
                }
                else
                {
                    variables[mapping.VariableName] = new Variable(mapping.VariableName, mapping.DataType, VariableSource.Response) { Value = converted };
                }
                Logger.LogInfo($"HttpResponseProcessor: extracted {mapping.VariableName} = {converted}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"HttpResponseProcessor: failed extracting variable {mapping.VariableName} from {mapping.JsonPointer}", ex);
                SetVariableDefaultValue(mapping.VariableName, mapping.DataType, variables);
            }
        }

        private void SetVariableDefaultValue(string variableName, VariableType dataType, Dictionary<string, Variable> variables)
        {
            var def = TypeConverter.GetDefaultValue(dataType);
            if (variables.ContainsKey(variableName)) variables[variableName].Value = def;
            else variables[variableName] = new Variable(variableName, dataType, VariableSource.Response) { Value = def };
        }

        public bool ValidateResponse(HttpResponseData response, HttpResponseTemplate template)
        {
            if (template.ExpectedStatusCode.HasValue)
            {
                if (response.StatusCode != template.ExpectedStatusCode.Value) return false;
            }
            if (template.Mappings.Count > 0 && string.IsNullOrWhiteSpace(response.Body)) return false;
            return true;
        }

        public string FormatResponse(HttpResponseData response)
        {
            if (response == null) return string.Empty;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"{response.StatusCode} {response.StatusMessage}");
            sb.AppendLine();
            if (response.Headers.Count > 0)
            {
                sb.AppendLine("Headers:");
                foreach (var h in response.Headers) sb.AppendLine($" {h.Key}: {h.Value}");
                sb.AppendLine();
            }
            if (!string.IsNullOrWhiteSpace(response.Body)) sb.AppendLine(_jsonProcessor.FormatJson(response.Body));
            if (!string.IsNullOrWhiteSpace(response.ErrorMessage)) { sb.AppendLine(); sb.AppendLine($"Error: {response.ErrorMessage}"); }
            Logger.LogInfo($"HttpResponseProcessor: FormatResponse called, status={response.StatusCode}");
            return sb.ToString();
        }
    }
}
