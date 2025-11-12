using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PLC2MES.Core.Models;
using PLC2MES.Core.Processors;
using PLC2MES.Core.Services;
using PLC2MES.Utils;

namespace PLC2MES.Core.Parsers
{
    // Added: Parses response templates and captures mapping metadata (headers + body).
    public class ResponseTemplateParser
    {
        private readonly JsonProcessor _jsonProcessor = new JsonProcessor();
        private readonly IVariableManager _vars;

        public ResponseTemplateParser(IVariableManager vars)
        {
            _vars = vars ?? throw new ArgumentNullException(nameof(vars));
        }

        // Added: Entry point that splits header/body and builds the mapping collections.
        public HttpResponseTemplate Parse(string templateText)
        {
            Logger.LogInfo("ResponseTemplateParser: Parse called");
            if (string.IsNullOrWhiteSpace(templateText)) throw new ArgumentException("响应模板文本不能为空");

            var template = new HttpResponseTemplate { OriginalText = templateText };
            string[] parts = SplitHeaderAndBody(templateText);
            string headerSection = parts[0];
            string bodySection = parts.Length > 1 ? parts[1] : string.Empty;

            ParseHeaderSection(headerSection, template);
            if (!string.IsNullOrWhiteSpace(bodySection))
                ParseBodySection(bodySection, template);

            Logger.LogInfo($"ResponseTemplateParser: headerMappings={template.HeaderMappings.Count}, bodyMappings={template.BodyMappings.Count}");
            return template;
        }

        private string[] SplitHeaderAndBody(string text)
        {
            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            int emptyIndex = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                {
                    emptyIndex = i;
                    break;
                }
            }
            if (emptyIndex == -1) return new[] { text };

            var header = new StringBuilder();
            var body = new StringBuilder();
            for (int i = 0; i < emptyIndex; i++) header.AppendLine(lines[i]);
            for (int i = emptyIndex + 1; i < lines.Length; i++) body.AppendLine(lines[i]);
            return new[] { header.ToString().Trim(), body.ToString().Trim() };
        }

        private void ParseHeaderSection(string headerSection, HttpResponseTemplate template)
        {
            var lines = headerSection.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0) throw new Exception("响应模板格式错误：缺少状态行");

            ParseStatusLine(lines[0], template);
            for (int i = 1; i < lines.Length; i++)
                ParseHeaderLine(lines[i], template);
        }

        private void ParseStatusLine(string line, HttpResponseTemplate template)
        {
            var match = Regex.Match(line, RegexPatterns.StatusLine);
            if (match.Success) template.ExpectedStatusCode = int.Parse(match.Groups[1].Value);
        }

        // Added: Parses header variables and builds regex-based capture mappings.
        private void ParseHeaderLine(string line, HttpResponseTemplate template)
        {
            var match = Regex.Match(line, RegexPatterns.HeaderLine);
            if (!match.Success) return;

            string key = match.Groups[1].Value.Trim();
            string value = match.Groups[2].Value.Trim();
            template.Headers[key] = value;

            try
            {
                var varRegex = new Regex(RegexPatterns.Variable, RegexOptions.Compiled);
                var matches = varRegex.Matches(value);
                if (matches.Count == 0)
                {
                    var hvMatch = Regex.Match(value, "^\\s*" + RegexPatterns.Variable + "\\s*$");
                    if (hvMatch.Success)
                    {
                        // Added: Simple header mapping with a single capture group.
                        string varName = hvMatch.Groups["var"].Value;
                        var mapping = new ResponseHeaderMapping
                        {
                            HeaderName = key,
                            VariableName = varName,
                            VariableType = VariableType.CreateScalar(VariableKind.String),
                            GroupIndex = 1,
                            RegexPattern = "^(.+?)$"
                        };
                        template.HeaderMappings.Add(mapping);
                        _vars.RegisterVariable(new Variable(varName, mapping.VariableType, VariableSource.Response));
                    }
                    return;
                }

                var tempMappings = new List<ResponseHeaderMapping>();
                var patternBuilder = new StringBuilder();
                int lastIndex = 0;
                int groupIndex = 1;

                foreach (Match varMatch in matches)
                {
                    var between = value.Substring(lastIndex, varMatch.Index - lastIndex);
                    patternBuilder.Append(Regex.Escape(between));
                    patternBuilder.Append("(.+?)");

                    string varName = varMatch.Groups["var"].Value;
                    string typeStr = varMatch.Groups["type"].Success ? varMatch.Groups["type"].Value : null;
                    var varType = string.IsNullOrEmpty(typeStr) ? VariableType.CreateScalar(VariableKind.String) : ParseVariableType(typeStr);

                    // Added: Capture metadata for each placeholder so we can build the final regex in one pass.
                    tempMappings.Add(new ResponseHeaderMapping
                    {
                        HeaderName = key,
                        GroupIndex = groupIndex,
                        VariableName = varName,
                        VariableType = varType
                    });

                    groupIndex++;
                    lastIndex = varMatch.Index + varMatch.Length;
                }

                if (lastIndex < value.Length)
                {
                    var tail = value.Substring(lastIndex);
                    patternBuilder.Append(Regex.Escape(tail));
                }

                string finalPattern = "^" + patternBuilder + "$";
                foreach (var map in tempMappings)
                {
                    map.RegexPattern = finalPattern;
                    template.HeaderMappings.Add(map);
                    _vars.RegisterVariable(new Variable(map.VariableName, map.VariableType, VariableSource.Response));
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"ResponseTemplateParser: failed to parse header line '{line}'", ex);
            }
        }

        // Added: Replaces body placeholders with quoted markers and records JSON pointers.
        private void ParseBodySection(string bodySection, HttpResponseTemplate template)
        {
            var expressions = new List<TemplateExpression>();
            var varRegex = new Regex(RegexPatterns.Variable, RegexOptions.Compiled);
            string processed = varRegex.Replace(bodySection, match =>
            {
                string typeStr = match.Groups["type"].Success ? match.Groups["type"].Value : null;
                string varName = match.Groups["var"].Value;
                string format = match.Groups["format"].Success ? match.Groups["format"].Value : null;
                var varType = string.IsNullOrEmpty(typeStr) ? VariableType.CreateScalar(VariableKind.String) : ParseVariableType(typeStr);

                var expr = new TemplateExpression
                {
                    Id = StringHelper.GenerateUniqueId(),
                    VariableName = varName,
                    VariableType = varType,
                    FormatString = format,
                    OriginalText = match.Value,
                    Location = ExpressionLocation.Body
                };
                expressions.Add(expr);
                _vars.RegisterVariable(new Variable(varName, varType, VariableSource.Response));

                // Added: Always quote placeholders so the JSON stays valid before we replace nodes.
                return "\"" + StringHelper.CreatePlaceholder(expr.Id) + "\"";
            });

            template.BodyTemplate = processed;

            try
            {
                var rootNode = _jsonProcessor.ParseJson(processed);
                // Added: Index expressions by placeholder id so lookups stay O(1) during traversal.
                var expressionIndex = expressions.ToDictionary(e => e.Id, e => e);

                _jsonProcessor.TraverseJson(rootNode, "", (path, value) =>
                {
                    if (value is not string asString || !StringHelper.IsPlaceholder(asString)) return;
                    var id = StringHelper.ExtractIdFromPlaceholder(asString);
                    if (id == null || !expressionIndex.TryGetValue(id, out var expression)) return;

                    // Added: Create mapping per placeholder and decide whether it is part of an array projection.
                    var mapping = new ResponseBodyMapping
                    {
                        VariableName = expression.VariableName,
                        VariableType = NormalizeVariableType(expression.VariableType)
                    };

                    var projection = TemplateArrayHelper.TryDetectArrayProjection(rootNode, path);
                    if (projection != null)
                    {
                        mapping.Projection = projection;
                        mapping.VariableType = PromoteToArrayType(mapping.VariableType);
                        expression.VariableType = mapping.VariableType;
                    }
                    else
                    {
                        mapping.Pointer = path;
                    }

                    template.BodyMappings.Add(mapping);
                    _vars.RegisterVariable(new Variable(expression.VariableName, mapping.VariableType, VariableSource.Response));
                });
            }
            catch (Exception ex)
            {
                Logger.LogError("ResponseTemplateParser: ParseBodySection failed", ex);
                throw new Exception($"响应Body JSON解析失败: {ex.Message}");
            }
        }

        private VariableType ParseVariableType(string typeStr)
        {
            bool _;
            return ParseVariableType(typeStr, out _);
        }

        private VariableType ParseVariableType(string typeStr, out bool isArray)
        {
            isArray = false;
            if (string.IsNullOrEmpty(typeStr)) return VariableType.CreateScalar(VariableKind.String);

            var trimmed = typeStr.Trim();
            if (trimmed.EndsWith("[]"))
            {
                isArray = true;
                trimmed = trimmed[..^2];
            }
            else if (trimmed.StartsWith("Array<") && trimmed.EndsWith(">"))
            {
                isArray = true;
                trimmed = trimmed.Substring(6, trimmed.Length - 7);
            }

            VariableKind kind = trimmed.ToLower() switch
            {
                "bool" => VariableKind.Bool,
                "int" => VariableKind.Int,
                "number" => VariableKind.Int,
                "float" => VariableKind.Float,
                "string" => VariableKind.String,
                "datetime" => VariableKind.DateTime,
                _ => throw new Exception($"不支持的数据类型: {typeStr}")
            };

            var scalar = VariableType.CreateScalar(kind);
            return isArray ? VariableType.CreateArray(scalar) : scalar;
        }

        private VariableType NormalizeVariableType(VariableType type)
        {
            // Added: Ensure downstream logic never sees null variable types.
            return type ?? VariableType.CreateScalar(VariableKind.String);
        }

        private VariableType PromoteToArrayType(VariableType type)
        {
            if (type != null && type.IsArray) return type;
            var elementType = NormalizeVariableType(type);
            return VariableType.CreateArray(elementType);
        }
    }
}
