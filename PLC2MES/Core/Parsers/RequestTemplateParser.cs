using System;
using System.Text;
using System.Text.RegularExpressions;
using PLC2MES.Core.Models;
using PLC2MES.Utils;
using System.Collections.Generic;

namespace PLC2MES.Core.Parsers
{
    public class RequestTemplateParser
    {
        public HttpRequestTemplate Parse(string templateText)
        {
            if (string.IsNullOrWhiteSpace(templateText)) throw new ArgumentException("模板文本不能为空");
            StringHelper.ResetIdCounter();
            var template = new HttpRequestTemplate { OriginalText = templateText };
            string[] parts = SplitHeaderAndBody(templateText);
            string headerSection = parts[0];
            string bodySection = parts.Length > 1 ? parts[1] : string.Empty;
            ParseHeaderSection(headerSection, template);
            if (!string.IsNullOrWhiteSpace(bodySection)) ParseBodySection(bodySection, template);
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

        private void ParseHeaderSection(string headerSection, HttpRequestTemplate template)
        {
            var lines = headerSection.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0) throw new Exception("请求模板格式错误：缺少请求行");
            ParseRequestLine(lines[0], template);
            for (int i = 1; i < lines.Length; i++) ParseHeaderLine(lines[i], template);
        }

        private void ParseRequestLine(string line, HttpRequestTemplate template)
        {
            var m = Regex.Match(line, RegexPatterns.RequestLine);
            if (!m.Success) throw new Exception($"请求行格式错误: {line}");
            template.Method = m.Groups[1].Value;
            string urlPart = m.Groups[2].Value;
            template.Url = ProcessUrlVariables(urlPart, template);
        }

        private string ProcessUrlVariables(string url, HttpRequestTemplate template)
        {
            return Regex.Replace(url, RegexPatterns.UrlVariable, match =>
            {
                string varName = match.Groups[1].Value;
                string format = match.Groups[2].Success ? match.Groups[2].Value : null;
                var expression = new TemplateExpression { Id = StringHelper.GenerateUniqueId(), VariableName = varName, DataType = null, FormatString = format, OriginalText = match.Value, Location = ExpressionLocation.Url };
                template.Expressions.Add(expression);
                return match.Value;
            });
        }

        private void ParseHeaderLine(string line, HttpRequestTemplate template)
        {
            var m = Regex.Match(line, RegexPatterns.HeaderLine);
            if (!m.Success) return;
            string key = m.Groups[1].Value.Trim(); string value = m.Groups[2].Value.Trim();
            value = ProcessHeaderVariables(value, template);
            template.Headers[key] = value;
        }

        private string ProcessHeaderVariables(string headerValue, HttpRequestTemplate template)
        {
            return Regex.Replace(headerValue, RegexPatterns.HeaderVariable, match =>
            {
                string varName = match.Groups[1].Value; string format = match.Groups[2].Success ? match.Groups[2].Value : null;
                var expression = new TemplateExpression { Id = StringHelper.GenerateUniqueId(), VariableName = varName, DataType = null, FormatString = format, OriginalText = match.Value, Location = ExpressionLocation.Header };
                template.Expressions.Add(expression);
                return match.Value;
            });
        }

        private void ParseBodySection(string bodySection, HttpRequestTemplate template)
        {
            string processed = ProcessBodyVariables(bodySection, template);
            template.BodyTemplate = processed;
        }

        private string ProcessBodyVariables(string body, HttpRequestTemplate template)
        {
            return Regex.Replace(body, RegexPatterns.BodyVariable, match =>
            {
                string typeStr = match.Groups[1].Value; string varName = match.Groups[2].Value; string format = match.Groups[3].Success ? match.Groups[3].Value : null;
                VariableType varType = ParseVariableType(typeStr);
                var expression = new TemplateExpression { Id = StringHelper.GenerateUniqueId(), VariableName = varName, DataType = varType, FormatString = format, OriginalText = match.Value, Location = ExpressionLocation.Body };
                template.Expressions.Add(expression);
                // Return quoted placeholder so the JSON remains valid
                return "\"" + StringHelper.CreatePlaceholder(expression.Id) + "\"";
            });
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