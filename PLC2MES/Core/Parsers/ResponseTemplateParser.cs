using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using PLC2MES.Core.Models;
using PLC2MES.Core.Processors;
using PLC2MES.Utils;

namespace PLC2MES.Core.Parsers
{
    public class ResponseTemplateParser
    {
        private JsonProcessor _jsonProcessor;

        public ResponseTemplateParser()
        {
            _jsonProcessor = new JsonProcessor();
        }

        public HttpResponseTemplate Parse(string templateText)
        {
            Logger.LogInfo("ResponseTemplateParser: Parse called");
            try
            {
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
            catch (Exception ex)
            {
                Logger.LogError("ResponseTemplateParser: Parse failed", ex);
                throw;
            }
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
                // Find placeholders in header value - support both @Type(var) and @(var)
                var combined = new Regex("(" + RegexPatterns.BodyVariable + ")|(" + RegexPatterns.HeaderVariable + ")", RegexOptions.Compiled);
                var matches = combined.Matches(value);
                if (matches.Count == 0)
                {
                    // If the entire value is exactly a simple @(Var) treat as whole-value mapping (legacy behavior)
                    var hvMatch = Regex.Match(value, "^\\s*" + RegexPatterns.HeaderVariable + "\\s*$");
                    if (hvMatch.Success)
                    {
                        string varName = hvMatch.Groups[1].Value;
                        string format = hvMatch.Groups[2].Success ? hvMatch.Groups[2].Value : null;
                        var mapping = new ResponseMapping
                        {
                            Id = StringHelper.GenerateUniqueId(),
                            HeaderName = key,
                            VariableName = varName,
                            DataType = VariableType.String
                        };
                        template.Mappings.Add(mapping);
                    }
                    return;
                }

                // Build a regex pattern by escaping static parts and inserting capture groups for placeholders
                var patternBuilder = new StringBuilder();
                int lastIndex = 0;
                int groupIndex = 1; // capture group numbering starts at 1

                foreach (Match match in matches)
                {
                    // append escaped substring between lastIndex and match.Index
                    var between = value.Substring(lastIndex, match.Index - lastIndex);
                    patternBuilder.Append(Regex.Escape(between));

                    // insert a non-greedy capture group
                    patternBuilder.Append("(.+?)");

                    // determine placeholder info and create mapping for this placeholder
                    string varName = null;
                    VariableType dataType = VariableType.String;

                    // check which group matched
                    if (match.Groups[1].Success)
                    {
                        // BodyVariable matched: groups:1(type),2(varName),3(format) depending on pattern grouping
                        // But because combined groups, extract inner groups via inner match
                        var inner = Regex.Match(match.Value, RegexPatterns.BodyVariable);
                        if (inner.Success)
                        {
                            var typeStr = inner.Groups[1].Value;
                            varName = inner.Groups[2].Value;
                            var fmt = inner.Groups[3].Success ? inner.Groups[3].Value : null;
                            dataType = ParseVariableType(typeStr);
                        }
                    }
                    else if (match.Groups[4].Success || match.Groups[2].Success)
                    {
                        // HeaderVariable matched
                        var inner = Regex.Match(match.Value, RegexPatterns.HeaderVariable);
                        if (inner.Success)
                        {
                            varName = inner.Groups[1].Value;
                            var fmt = inner.Groups[2].Success ? inner.Groups[2].Value : null;
                            dataType = VariableType.String;
                        }
                    }

                    // Create mapping referencing the header regex and the group index
                    var map = new ResponseMapping
                    {
                        Id = StringHelper.GenerateUniqueId(),
                        HeaderName = key,
                        HeaderRegex = patternBuilder.ToString() + ".*", // allow rest of header to remain
                        HeaderGroupIndex = groupIndex,
                        VariableName = varName,
                        DataType = dataType
                    };
                    template.Mappings.Add(map);

                    groupIndex++;
                    lastIndex = match.Index + match.Length;
                }

                // append trailing part
                if (lastIndex < value.Length)
                {
                    var tail = value.Substring(lastIndex);
                    patternBuilder.Append(Regex.Escape(tail));
                }

                // Finalize header regex for mappings that were added referencing the same header
                string finalPattern = patternBuilder.ToString();
                // update HeaderRegex for mappings that were just added for this header
                int startUpdate = template.Mappings.Count - (groupIndex - 1);
                for (int i = startUpdate; i < template.Mappings.Count; i++)
                {
                    template.Mappings[i].HeaderRegex = finalPattern;
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
            string processed = Regex.Replace(bodySection, RegexPatterns.BodyVariable, match =>
            {
                string typeStr = match.Groups[1].Value; string varName = match.Groups[2].Value; string format = match.Groups[3].Success ? match.Groups[3].Value : null;
                VariableType varType = ParseVariableType(typeStr);
                var expr = new TemplateExpression { Id = StringHelper.GenerateUniqueId(), VariableName = varName, DataType = varType, FormatString = format, OriginalText = match.Value, Location = ExpressionLocation.Body };
                expressions.Add(expr);
                return "\"" + StringHelper.CreatePlaceholder(expr.Id) + "\"";
            });
            template.BodyTemplate = processed;

            // Parse JSON and traverse to find placeholders
            try
            {
                var node = _jsonProcessor.ParseJson(processed);
                _jsonProcessor.TraverseJson(node, "", (path, value) =>
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
                                    var mapping = new ResponseMapping { Id = id, JsonPointer = path, VariableName = expression.VariableName, DataType = expression.DataType.Value };
                                    template.Mappings.Add(mapping);
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
            switch (typeStr.ToLower())
            {
                case "bool": return VariableType.Bool;
                case "int":
                case "number": return VariableType.Int;
                case "float": return VariableType.Float;
                case "string": return VariableType.String;
                case "datetime": return VariableType.DateTime;
                default: throw new Exception($"不支持的数据类型: {typeStr}");
            }
        }
    }
}