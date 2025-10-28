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