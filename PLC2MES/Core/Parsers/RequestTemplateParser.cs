using System;
using System.Text;
using System.Text.RegularExpressions;
using PLC2MES.Core.Models;
using PLC2MES.Core.Services;
using PLC2MES.Utils;
using System.Collections.Generic;

namespace PLC2MES.Core.Parsers
{
    public class RequestTemplateParser
    {
        private readonly IVariableManager _vars;
        public RequestTemplateParser(IVariableManager vars) { _vars = vars ?? throw new ArgumentNullException(nameof(vars)); }

        public HttpRequestTemplate Parse(string templateText)
        {
            Logger.LogInfo("RequestTemplateParser: Parse called");
            if (string.IsNullOrWhiteSpace(templateText)) throw new ArgumentException("模板文本不能为空");
            StringHelper.ResetIdCounter();
            var template = new HttpRequestTemplate { OriginalText = templateText };
            string[] parts = SplitHeaderAndBody(templateText);
            string headerSection = parts[0];
            string bodySection = parts.Length > 1 ? parts[1] : string.Empty;
            ParseHeaderSection(headerSection, template);
            if (!string.IsNullOrWhiteSpace(bodySection)) ParseBodySection(bodySection, template);
            Logger.LogInfo($"RequestTemplateParser: Parse finished, expressions={template.Expressions.Count}");
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
            var varRegex = new Regex(RegexPatterns.Variable, RegexOptions.Compiled);
            return varRegex.Replace(url, match =>
            {
                string varName = match.Groups["var"].Value;
                string format = match.Groups["format"].Success ? match.Groups["format"].Value : null;
                var expression = new TemplateExpression { Id = StringHelper.GenerateUniqueId(), VariableName = varName, DataType = VariableType.CreateScalar(VariableKind.String), FormatString = format, OriginalText = match.Value, Location = ExpressionLocation.Url };
                template.Expressions.Add(expression);
                // register variable as string for URL
                _vars.RegisterVariable(new Variable(varName, VariableType.CreateScalar(VariableKind.String), VariableSource.Request, format));
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
            var varRegex = new Regex(RegexPatterns.Variable, RegexOptions.Compiled);
            return varRegex.Replace(headerValue, match =>
            {
                string varName = match.Groups["var"].Value;
                string format = match.Groups["format"].Success ? match.Groups["format"].Value : null;
                var expression = new TemplateExpression { Id = StringHelper.GenerateUniqueId(), VariableName = varName, DataType = VariableType.CreateScalar(VariableKind.String), FormatString = format, OriginalText = match.Value, Location = ExpressionLocation.Header };
                template.Expressions.Add(expression);
                _vars.RegisterVariable(new Variable(varName, VariableType.CreateScalar(VariableKind.String), VariableSource.Request, format));
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
            var varRegex = new Regex(RegexPatterns.Variable, RegexOptions.Compiled);
            return varRegex.Replace(body, match =>
            {
                string typeStr = match.Groups["type"].Success ? match.Groups["type"].Value : null;
                string varName = match.Groups["var"].Value;
                string format = match.Groups["format"].Success ? match.Groups["format"].Value : null;

                bool isArray;
                var elemKindType = ParseVariableType(typeStr, out isArray);
                VariableType varType = isArray ? VariableType.CreateArray(VariableType.CreateScalar(elemKindType)) : VariableType.CreateScalar(elemKindType);

                var expression = new TemplateExpression { Id = StringHelper.GenerateUniqueId(), VariableName = varName, DataType = isArray ? varType.ElementType : varType, FormatString = format, OriginalText = match.Value, Location = ExpressionLocation.Body };
                template.Expressions.Add(expression);

                // register variable: use VariableType (array or scalar)
                var variable = new Variable(varName, varType, VariableSource.Request, format);
                _vars.RegisterVariable(variable);

                // For arrays we must not quote the placeholder (so replacement will produce a JSON array later)
                if (isArray) return StringHelper.CreatePlaceholder(expression.Id);

                // Return quoted placeholder so the JSON remains valid for scalars
                return "\"" + StringHelper.CreatePlaceholder(expression.Id) + "\"";
            });
        }

        private VariableKind ParseVariableType(string typeStr, out bool isArray)
        {
            isArray = false;
            if (string.IsNullOrEmpty(typeStr)) return VariableKind.String;

            // support Array<Elem> or Elem[]
            var s = typeStr.Trim();
            if (s.EndsWith("[]"))
            {
                isArray = true;
                s = s.Substring(0, s.Length -2);
            }
            else if (s.StartsWith("Array<") && s.EndsWith(">"))
            {
                isArray = true;
                s = s.Substring(6, s.Length -7);
            }

            switch (s.ToLower())
            {
                case "bool": return VariableKind.Bool;
                case "int":
                case "number": return VariableKind.Int;
                case "float": return VariableKind.Float;
                case "string": return VariableKind.String;
                case "datetime": return VariableKind.DateTime;
                default: throw new Exception($"不支持的数据类型: {typeStr}");
            }
        }
    }
}