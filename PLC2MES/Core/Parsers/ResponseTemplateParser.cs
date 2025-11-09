using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json.Nodes;
using PLC2MES.Core.Models;
using PLC2MES.Core.Processors;
using PLC2MES.Core.Services;
using PLC2MES.Utils;

namespace PLC2MES.Core.Parsers
{
    public class ResponseTemplateParser
    {
        private JsonProcessor _jsonProcessor;
        private readonly IVariableManager _vars;

        public ResponseTemplateParser(IVariableManager vars)
        {
            _jsonProcessor = new JsonProcessor();
            _vars = vars ?? throw new ArgumentNullException(nameof(vars));
        }

        public HttpResponseTemplate Parse(string templateText)
        {
            Logger.LogInfo("ResponseTemplateParser: Parse called");
            if (string.IsNullOrWhiteSpace(templateText)) throw new ArgumentException("响应模板文本不能为空");
            var template = new HttpResponseTemplate { OriginalText = templateText };
            string[] parts = SplitHeaderAndBody(templateText);
            string headerSection = parts[0];
            string bodySection = parts.Length > 1 ? parts[1] : string.Empty;
            ParseHeaderSection(headerSection, template);
            if (!string.IsNullOrWhiteSpace(bodySection)) ParseBodySection(bodySection, template);
            Logger.LogInfo($"ResponseTemplateParser: Parse finished, mappings={template.Mappings.Count}");
            return template;
        }

        private string[] SplitHeaderAndBody(string text)
        {
            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            int emptyIndex = -1;
            for (int i = 0; i < lines.Length; i++) if (string.IsNullOrWhiteSpace(lines[i])) { emptyIndex = i; break; }
            if (emptyIndex == -1) return new[] { text };
            var header = new StringBuilder(); var body = new StringBuilder();
            for (int i = 0; i < emptyIndex; i++) header.AppendLine(lines[i]);
            for (int i = emptyIndex + 1; i < lines.Length; i++) body.AppendLine(lines[i]);
            return new[] { header.ToString().Trim(), body.ToString().Trim() };
        }

        private void ParseHeaderSection(string headerSection, HttpResponseTemplate template)
        {
            var lines = headerSection.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0) throw new Exception("响应模板格式错误：缺少状态行");
            ParseStatusLine(lines[0], template);
            for (int i = 1; i < lines.Length; i++) ParseHeaderLine(lines[i], template);
        }

        private void ParseStatusLine(string line, HttpResponseTemplate template)
        {
            var m = Regex.Match(line, RegexPatterns.StatusLine);
            if (m.Success) template.ExpectedStatusCode = int.Parse(m.Groups[1].Value);
        }

        private void ParseHeaderLine(string line, HttpResponseTemplate template)
        {
            var m = Regex.Match(line, RegexPatterns.HeaderLine);
            if (!m.Success) return;
            string key = m.Groups[1].Value.Trim(); string value = m.Groups[2].Value.Trim();
            template.Headers[key] = value;

            try
            {
                // Use unified variable regex with named groups
                var varRegex = new Regex(RegexPatterns.Variable, RegexOptions.Compiled);
                var matches = varRegex.Matches(value);
                if (matches.Count == 0)
                {
                    // If the entire value is exactly a simple @(Var) treat as whole-value mapping (legacy behavior)
                    var hvMatch = Regex.Match(value, "^\\s*" + RegexPatterns.Variable + "\\s*$");
                    if (hvMatch.Success)
                    {
                        string varName = hvMatch.Groups["var"].Value;
                        var mapping = new ResponseMapping
                        {
                            Id = StringHelper.GenerateUniqueId(),
                            HeaderName = key,
                            VariableName = varName,
                            DataType = VariableType.CreateScalar(VariableKind.String),
                            HeaderGroupIndex = 1,
                            HeaderRegex = "^(.+?)$"
                        };
                        template.Mappings.Add(mapping);
                        _vars.RegisterVariable(new Variable(varName, VariableType.CreateScalar(VariableKind.String), VariableSource.Response));
                    }
                    return;
                }

                // Build a regex pattern by escaping static parts and inserting capture groups for placeholders
                var patternBuilder = new StringBuilder();
                int lastIndex = 0;
                int groupIndex = 1; // capture group numbering starts at 1

                // Temp list to hold mapping info until final pattern is known
                var tempMappings = new List<ResponseMapping>();

                foreach (Match match in matches)
                {
                    // append escaped substring between lastIndex and match.Index
                    var between = value.Substring(lastIndex, match.Index - lastIndex);
                    patternBuilder.Append(Regex.Escape(between));

                    // insert a non-greedy capture group
                    patternBuilder.Append("(.+?)");

                    // extract placeholder info via named groups
                    string varName = match.Groups["var"].Value;
                    string typeStr = match.Groups["type"].Success ? match.Groups["type"].Value : null;

                    VariableType dataType = VariableType.CreateScalar(VariableKind.String);
                    if (!string.IsNullOrEmpty(typeStr)) dataType = ParseVariableType(typeStr);

                    // create temp mapping referencing the group index
                    var map = new ResponseMapping
                    {
                        Id = StringHelper.GenerateUniqueId(),
                        HeaderName = key,
                        HeaderGroupIndex = groupIndex,
                        VariableName = varName,
                        DataType = dataType
                    };
                    tempMappings.Add(map);

                    groupIndex++;
                    lastIndex = match.Index + match.Length;
                }

                // append trailing part
                if (lastIndex < value.Length)
                {
                    var tail = value.Substring(lastIndex);
                    patternBuilder.Append(Regex.Escape(tail));
                }

                // Finalize header regex for mappings that were added for this header
                string finalPattern = "^" + patternBuilder.ToString() + "$";

                foreach (var map in tempMappings)
                {
                    map.HeaderRegex = finalPattern;
                    template.Mappings.Add(map);

                    // Register variable: if mapping.DataType is array type, Variable constructor will set default accordingly
                    var v = new Variable(map.VariableName, map.DataType, VariableSource.Response);
                    _vars.RegisterVariable(v);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"ResponseTemplateParser: failed to parse header line '{line}' for mappings", ex);
            }
        }

        private void ParseBodySection(string bodySection, HttpResponseTemplate template)
        {
            // Extract expressions in body and replace with quoted placeholders
            var expressions = new List<TemplateExpression>();
            var varRegex = new Regex(RegexPatterns.Variable, RegexOptions.Compiled);
            string processed = varRegex.Replace(bodySection, match =>
            {
                string typeStr = match.Groups["type"].Success ? match.Groups["type"].Value : null;
                string varName = match.Groups["var"].Value;
                string format = match.Groups["format"].Success ? match.Groups["format"].Value : null;

                VariableType varType = string.IsNullOrEmpty(typeStr) ? VariableType.CreateScalar(VariableKind.String) : ParseVariableType(typeStr);

                var expr = new TemplateExpression { Id = StringHelper.GenerateUniqueId(), VariableName = varName, DataType = varType, FormatString = format, OriginalText = match.Value, Location = ExpressionLocation.Body };
                expressions.Add(expr);

                var v = new Variable(varName, varType, VariableSource.Response);
                _vars.RegisterVariable(v);

                // 始终包裹引号，保证模板文本永远是合法 JSON，后续解析才不会报错
                return "\"" + StringHelper.CreatePlaceholder(expr.Id) + "\"";
            });
            template.BodyTemplate = processed;

            // Parse JSON and traverse to find placeholders
            try
            {
                var rootNode = _jsonProcessor.ParseJson(processed);
                _jsonProcessor.TraverseJson(rootNode, "", (path, value) =>
                {
                    try
                    {
                        if (value is string s && StringHelper.IsPlaceholder(s))
                        {
                            var id = StringHelper.ExtractIdFromPlaceholder(s);
                            if (id != null)
                            {
                                var expression = expressions.Find(e => e.Id == id);
                                if (expression != null)
                                {
                                    var mapping = new ResponseMapping { Id = id, JsonPointer = path, VariableName = expression.VariableName, DataType = expression.DataType };
                                    if (expression.DataType != null && expression.DataType.IsArray)
                                    {
                                        // 如果指向数组元素内部，则改走“数组投影”逻辑
                                        TryConfigureArrayProjectionMapping(rootNode, path, expression, mapping);
                                    }
                                    template.Mappings.Add(mapping);
                                    _vars.RegisterVariable(new Variable(expression.VariableName, expression.DataType, VariableSource.Response));
                                }
                            }
                        }
                    }
                    catch (Exception exInner)
                    {
                        Logger.LogError($"ResponseTemplateParser: error while traversing json path={path}", exInner);
                    }
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
            bool dummy;
            return ParseVariableType(typeStr, out dummy);
        }

        /// <summary>
        /// 如果 @Array<T> 出现在数组元素内部，将 mapping 转换为“数组投影”形式
        /// </summary>
        private bool TryConfigureArrayProjectionMapping(JsonNode rootNode, string placeholderPath, TemplateExpression expression, ResponseMapping mapping)
        {
            if (rootNode == null || string.IsNullOrEmpty(placeholderPath) || expression?.DataType == null || !expression.DataType.IsArray)
                return false;

            // 例如 /preferences/0/category 会被拆成 ["preferences","0","category"]
            var segments = JsonProcessor.SplitPointerSegments(placeholderPath);
            if (segments.Length == 0) return false;

            for (int i = segments.Length - 1; i >= 0; i--)
            {
                // 只要某一段是纯数字，就视为数组下标，从尾部向前找最近的数组
                if (!int.TryParse(segments[i], out _)) continue;

                // 找到最近的数组节点，并把其余路径当作元素内路径
                var collectionPointer = JsonProcessor.BuildPointerFromSegments(segments.Take(i), includeLeadingSlash: true, emptyResultAsSlash: true);
                if (!_jsonProcessor.TryGetNodeByPointer(rootNode, collectionPointer, out var ancestor) || ancestor is not JsonArray)
                    continue;

                // 剩余的段就是元素内部路径，例如 ["category"]
                var relativeSegments = segments.Skip(i + 1);
                mapping.CollectionPointer = collectionPointer;
                mapping.ElementRelativePointer = JsonProcessor.BuildPointerFromSegments(relativeSegments, includeLeadingSlash: true, emptyResultAsSlash: false);
                mapping.JsonPointer = null;
                return true;
            }

            return false;
        }

        private VariableType ParseVariableType(string typeStr, out bool isArray)
        {
            isArray = false;
            if (string.IsNullOrEmpty(typeStr)) return VariableType.CreateScalar(VariableKind.String);

            var s = typeStr.Trim();
            if (s.EndsWith("[]"))
            {
                isArray = true;
                s = s.Substring(0, s.Length - 2);
            }
            else if (s.StartsWith("Array<") && s.EndsWith(">"))
            {
                isArray = true;
                s = s.Substring(6, s.Length - 7);
            }

            VariableKind kind;
            switch (s.ToLower())
            {
                case "bool": kind = VariableKind.Bool; break;
                case "int":
                case "number": kind = VariableKind.Int; break;
                case "float": kind = VariableKind.Float; break;
                case "string": kind = VariableKind.String; break;
                case "datetime": kind = VariableKind.DateTime; break;
                default: throw new Exception($"不支持的数据类型: {typeStr}");
            }

            var scalar = VariableType.CreateScalar(kind);
            if (isArray) return VariableType.CreateArray(scalar);
            return scalar;
        }
    }
}
