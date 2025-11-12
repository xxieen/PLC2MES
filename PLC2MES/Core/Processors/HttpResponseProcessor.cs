using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text.Json.Nodes;
using PLC2MES.Core.Models;
using PLC2MES.Core.Services;
using PLC2MES.Utils;

namespace PLC2MES.Core.Processors
{
    public class HttpResponseProcessor
    {
        private JsonProcessor _jsonProcessor;
        private readonly IVariableManager _vars;
        public HttpResponseProcessor(IVariableManager vars)
        {
            _jsonProcessor = new JsonProcessor();
            _vars = vars ?? throw new ArgumentNullException(nameof(vars));
        }

        public void ProcessResponse(HttpResponseData response, HttpResponseTemplate template)
        {
            if (response == null || template == null) return;

            // ensure manager reference
            var manager = _vars;

            try
            {
                ProcessStatusCode(response, manager);
                ProcessHeaders(response, template, manager);
                ProcessBody(response, template, manager);
            }
            catch (Exception ex)
            {
                Logger.LogError("HttpResponseProcessor: ProcessResponse failed", ex);
                // do not rethrow - keep behavior tolerant
            }
        }

        private void ProcessStatusCode(HttpResponseData response, IVariableManager manager)
        {
            // set or register $StatusCode
            var existing = manager.GetVariable("$StatusCode");
            if (existing != null) manager.SetVariableValue("$StatusCode", response.StatusCode);
            else manager.RegisterVariable(new Variable("$StatusCode", VariableType.CreateScalar(VariableKind.Int), VariableSource.Response) { Value = response.StatusCode });
        }

        // Added: Header processing now iterates ResponseHeaderMapping records only.
        private void ProcessHeaders(HttpResponseData response, HttpResponseTemplate template, IVariableManager manager)
        {
            if (template?.HeaderMappings == null || template.HeaderMappings.Count == 0) return;

            // Added: Work with the slim header model so each mapping only holds header-related info.
            var headerGroups = template.HeaderMappings
                .GroupBy(m => m.HeaderName, StringComparer.OrdinalIgnoreCase);

            foreach (var hg in headerGroups)
            {
                string headerName = hg.Key;
                var mappings = hg.OrderBy(m => m.GroupIndex).ToList();

                // collect values for this header (may be multiple header lines)
                var values = new List<string>();
                if (response.Headers != null)
                {
                    foreach (var kv in response.Headers)
                    {
                        if (string.Equals(kv.Key, headerName, StringComparison.OrdinalIgnoreCase) && kv.Value != null)
                            values.AddRange(kv.Value);
                    }
                }

                if (values.Count ==0)
                {
                    // no header present -> set defaults
                    foreach (var m in mappings) SetVariableDefaultValue(m.VariableName, m.VariableType, manager);
                    continue;
                }

                // try to use mapping.HeaderRegex if provided; otherwise treat whole header value as the extracted value
                string pattern = mappings.First().RegexPattern;
                Regex rx = null;
                if (!string.IsNullOrWhiteSpace(pattern))
                {
                    try { rx = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline); }
                    catch (Exception ex) { Logger.LogError($"HttpResponseProcessor: invalid HeaderRegex for header {headerName}", ex); rx = null; }
                }

                bool anyMatched = false;

                foreach (var hv in values)
                {
                    if (rx != null)
                    {
                        var match = rx.Match(hv);
                        if (!match.Success) continue;
                        anyMatched = true;

                        foreach (var m in mappings)
                        {
                            try
                            {
                                int gi = m.GroupIndex <= 0 ? 1 : m.GroupIndex;
                                string captured = (gi < match.Groups.Count) ? match.Groups[gi].Value?.Trim() : null;
                                if (string.IsNullOrEmpty(captured))
                                {
                                    SetVariableDefaultValue(m.VariableName, m.VariableType, manager);
                                }
                                else
                                {
                                    var converted = TypeConverter.ConvertFromJson(captured, m.VariableType);
                                    SetOrRegisterVariable(m.VariableName, m.VariableType, converted, manager);
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError($"HttpResponseProcessor: extracting header mapping {m.VariableName} failed", ex);
                                SetVariableDefaultValue(m.VariableName, m.VariableType, manager);
                            }
                        }

                        // matched one header value - stop for this header
                        break;
                    }
                    else
                    {
                        // no regex: single mapping scenario or multiple mappings expecting same whole value
                        foreach (var m in mappings)
                        {
                            try
                            {
                                var converted = TypeConverter.ConvertFromJson(hv, m.VariableType);
                                SetOrRegisterVariable(m.VariableName, m.VariableType, converted, manager);
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError($"HttpResponseProcessor: converting header {headerName} for mapping {m.VariableName} failed", ex);
                                SetVariableDefaultValue(m.VariableName, m.VariableType, manager);
                            }
                        }

                        anyMatched = true;
                        break;
                    }
                }

                if (!anyMatched)
                {
                    // nothing matched -> defaults
                    foreach (var m in mappings) SetVariableDefaultValue(m.VariableName, m.VariableType, manager);
                }
            }
        }

        // Added: Body processing now branches between pointer reads and array projections using new mappings.
        private void ProcessBody(HttpResponseData response, HttpResponseTemplate template, IVariableManager manager)
        {
            if (template?.BodyMappings == null || template.BodyMappings.Count == 0 || string.IsNullOrWhiteSpace(response.Body)) return;

            System.Text.Json.Nodes.JsonNode node = null;
            try { node = _jsonProcessor.ParseJson(response.Body); }
            catch
            {
                Logger.LogInfo("HttpResponseProcessor: response body is not valid JSON; skipping body mappings");
                return;
            }

            // Added: Iterate using the new body mapping model so we can branch on array projections quickly.
            foreach (var m in template.BodyMappings)
            {
                try
                {
                    if (m.IsArrayProjection)
                    {
                        ProcessCollectionMapping(node, m, manager);
                        continue;
                    }

                    object val = null;
                    if (!string.IsNullOrWhiteSpace(m.Pointer))
                        val = _jsonProcessor.GetValueByPointer(node, m.Pointer);

                    if (val == null)
                    {
                        SetVariableDefaultValue(m.VariableName, m.VariableType, manager);
                    }
                    else
                    {
                        var converted = TypeConverter.ConvertFromJson(val, m.VariableType);
                        SetOrRegisterVariable(m.VariableName, m.VariableType, converted, manager);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"HttpResponseProcessor: failed extracting body mapping {m.VariableName}", ex);
                    SetVariableDefaultValue(m.VariableName, m.VariableType, manager);
                }
            }
        }

        private void SetOrRegisterVariable(string name, VariableType type, object value, IVariableManager manager)
        {
            var existing = manager.GetVariable(name);
            // determine if the extracted value is array-like
            bool valueIsArray = value is System.Collections.IEnumerable && !(value is string);
            if (existing != null)
            {
                // ensure variable type represents array if needed
                if (valueIsArray && !existing.Type.IsArray)
                {
                    existing.Type = VariableType.CreateArray(existing.Type);
                }
                manager.SetVariableValue(name, value);
            }
            else
            {
                // If value is array but provided type is scalar, wrap it
                var regType = type;
                if (valueIsArray && !regType.IsArray)
                {
                    regType = VariableType.CreateArray(regType);
                }
                var v = new Variable(name, regType, VariableSource.Response) { Value = value };
                manager.RegisterVariable(v);
            }
        }

        private void SetVariableDefaultValue(string variableName, VariableType dataType, IVariableManager manager)
        {
            var existing = manager.GetVariable(variableName);
            if (existing != null && existing.HasUserDefault)
            {
                manager.SetVariableValue(variableName, existing.GetEffectiveDefault());
                return;
            }

            var def = TypeConverter.GetDefaultValue(dataType);
            if (existing != null) manager.SetVariableValue(variableName, def);
            else manager.RegisterVariable(new Variable(variableName, dataType, VariableSource.Response) { Value = def });
        }

        public bool ValidateResponse(HttpResponseData response, HttpResponseTemplate template)
        {
            if (template.ExpectedStatusCode.HasValue)
            {
                if (response.StatusCode != template.ExpectedStatusCode.Value) return false;
            }
            if (template.BodyMappings.Count > 0 && string.IsNullOrWhiteSpace(response.Body)) return false;
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

        // Added: Aggregates array values using the projection info captured during parsing.
        private void ProcessCollectionMapping(JsonNode rootNode, ResponseBodyMapping mapping, IVariableManager manager)
        {
            try
            {
                // Added: Use projection metadata captured during parsing to aggregate an array.
                var collectionNode = _jsonProcessor.GetNodeByPointer(rootNode, mapping.Projection.CollectionPointer);
                if (collectionNode is not JsonArray jsonArray)
                {
                    SetVariableDefaultValue(mapping.VariableName, mapping.VariableType, manager);
                    return;
                }

                var elementType = mapping.VariableType?.ElementType ?? VariableType.CreateScalar(VariableKind.String);
                var results = new List<object>();

                foreach (var element in jsonArray)
                {
                    var rawValue = _jsonProcessor.GetValueFromNode(element, mapping.Projection.ElementPointer);
                    var convertedElement = TypeConverter.ConvertFromJson(rawValue, elementType);
                    results.Add(convertedElement);
                }

                SetOrRegisterVariable(mapping.VariableName, mapping.VariableType, results, manager);
            }
            catch (Exception ex)
            {
                Logger.LogError($"HttpResponseProcessor: failed extracting collection mapping {mapping.VariableName}", ex);
                SetVariableDefaultValue(mapping.VariableName, mapping.VariableType, manager);
            }
        }
    }
}
