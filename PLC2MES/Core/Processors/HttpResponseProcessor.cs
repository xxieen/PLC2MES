using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
            if (string.IsNullOrWhiteSpace(response.Body) && (template.Mappings == null || template.Mappings.Count ==0))
            {
                Logger.LogInfo("HttpResponseProcessor: empty response body and no mappings, nothing to extract");
                return;
            }
            try
            {
                System.Text.Json.Nodes.JsonNode node = null;
                if (!string.IsNullOrWhiteSpace(response.Body))
                {
                    node = _jsonProcessor.ParseJson(response.Body);
                    Logger.LogInfo($"HttpResponseProcessor: parsed JSON, mappings to process={template.Mappings.Count}");
                }
                else
                {
                    Logger.LogInfo($"HttpResponseProcessor: no body but mappings count={template.Mappings.Count}");
                }

                foreach (var mapping in template.Mappings)
                {
                    try
                    {
                        // If mapping targets a header
                        if (!string.IsNullOrWhiteSpace(mapping.HeaderName))
                        {
                            Logger.LogInfo($"HttpResponseProcessor: extracting header mapping {mapping.VariableName} from header '{mapping.HeaderName}'");
                            List<string> headerValues = null;
                            if (response.Headers != null)
                            {
                                var kv = response.Headers.FirstOrDefault(kvp => string.Equals(kvp.Key, mapping.HeaderName, StringComparison.OrdinalIgnoreCase));
                                if (!string.IsNullOrEmpty(kv.Key)) headerValues = kv.Value;
                            }
                            if (headerValues == null || headerValues.Count ==0)
                            {
                                Logger.LogInfo($"HttpResponseProcessor: header '{mapping.HeaderName}' not found, set default for {mapping.VariableName}");
                                SetVariableDefaultValue(mapping.VariableName, mapping.DataType, variables);
                            }
                            else
                            {
                                // If a header regex is provided, use it to extract from each header value
                                if (!string.IsNullOrWhiteSpace(mapping.HeaderRegex) && mapping.HeaderGroupIndex >0)
                                {
                                    try
                                    {
                                        var rx = new Regex(mapping.HeaderRegex, RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
                                        var extractedList = new List<string>();
                                        foreach (var hv in headerValues)
                                        {
                                            var matches = rx.Matches(hv);
                                            foreach (Match m in matches)
                                            {
                                                if (mapping.HeaderGroupIndex < m.Groups.Count)
                                                {
                                                    extractedList.Add(m.Groups[mapping.HeaderGroupIndex].Value);
                                                }
                                            }
                                        }
                                        if (extractedList.Count >0)
                                        {
                                            string capturedCombined = string.Join(",", extractedList);
                                            var converted = TypeConverter.ConvertFromJson(capturedCombined, mapping.DataType);
                                            if (variables.ContainsKey(mapping.VariableName)) variables[mapping.VariableName].Value = converted;
                                            else variables[mapping.VariableName] = new Variable(mapping.VariableName, mapping.DataType, VariableSource.Response) { Value = converted };
                                            Logger.LogInfo($"HttpResponseProcessor: extracted header (regex) {mapping.VariableName} = {capturedCombined}");
                                        }
                                        else
                                        {
                                            Logger.LogInfo($"HttpResponseProcessor: header regex did not match for header '{mapping.HeaderName}', set default for {mapping.VariableName}");
                                            SetVariableDefaultValue(mapping.VariableName, mapping.DataType, variables);
                                        }
                                    }
                                    catch (Exception exRx)
                                    {
                                        Logger.LogError($"HttpResponseProcessor: invalid header regex for mapping {mapping.VariableName}", exRx);
                                        SetVariableDefaultValue(mapping.VariableName, mapping.DataType, variables);
                                    }
                                }
                                else
                                {
                                    // Join multiple header values with comma and convert
                                    var combined = string.Join(",", headerValues);
                                    var converted = TypeConverter.ConvertFromJson(combined, mapping.DataType);
                                    if (variables.ContainsKey(mapping.VariableName)) variables[mapping.VariableName].Value = converted;
                                    else variables[mapping.VariableName] = new Variable(mapping.VariableName, mapping.DataType, VariableSource.Response) { Value = converted };
                                    Logger.LogInfo($"HttpResponseProcessor: extracted header {mapping.VariableName} = {combined}");
                                }
                            }
                        }
                        else
                        {
                            // body/json mapping
                            ExtractVariable(node, mapping, variables);
                        }
                    }
                    catch (Exception exMap)
                    {
                        Logger.LogError($"HttpResponseProcessor: failed processing mapping {mapping.VariableName}", exMap);
                        // set default on failure
                        SetVariableDefaultValue(mapping.VariableName, mapping.DataType, variables);
                    }
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
            // If the variable already exists and user provided a default, use it; otherwise use system default
            if (variables.ContainsKey(variableName) && variables[variableName].HasUserDefault)
            {
                variables[variableName].Value = variables[variableName].GetEffectiveDefault();
                Logger.LogInfo($"HttpResponseProcessor: set {variableName} to user default = {variables[variableName].Value}");
                return;
            }

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
            if (template.Mappings.Count >0 && string.IsNullOrWhiteSpace(response.Body)) return false;
            return true;
        }

        public string FormatResponse(HttpResponseData response)
        {
            if (response == null) return string.Empty;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"{response.StatusCode} {response.StatusMessage}");
            sb.AppendLine();
            if (response.Headers.Count >0)
            {
                sb.AppendLine("Headers:");
                foreach (var h in response.Headers)
                {
                    foreach (var v in h.Value)
                    {
                        sb.AppendLine($" {h.Key}: {v}");
                    }
                }
                sb.AppendLine();
            }
            if (!string.IsNullOrWhiteSpace(response.Body)) sb.AppendLine(_jsonProcessor.FormatJson(response.Body));
            if (!string.IsNullOrWhiteSpace(response.ErrorMessage)) { sb.AppendLine(); sb.AppendLine($"Error: {response.ErrorMessage}"); }
            Logger.LogInfo($"HttpResponseProcessor: FormatResponse called, status={response.StatusCode}");
            return sb.ToString();
        }
    }
}
