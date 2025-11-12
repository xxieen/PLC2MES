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
    // Added: Parses request templates and records JSON pointer metadata for body placeholders.
    public class RequestTemplateParser
    {
        private readonly JsonProcessor _jsonProcessor = new JsonProcessor();
        private readonly IVariableManager _vars;

        public RequestTemplateParser(IVariableManager vars)
        {
            _vars = vars ?? throw new ArgumentNullException(nameof(vars));
        }

        // Added: Main entry that splits header/body and kicks off parsing steps.
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
            if (!string.IsNullOrWhiteSpace(bodySection))
                ParseBodySection(bodySection, template);

            Logger.LogInfo($"RequestTemplateParser: expressions={template.Expressions.Count}, arrayTemplates={template.ArrayTemplates.Count}");
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

        private void ParseHeaderSection(string headerSection, HttpRequestTemplate template)
        {
            var lines = headerSection.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0) throw new Exception("请求模板格式错误：缺少请求行");

            ParseRequestLine(lines[0], template);
            for (int i = 1; i < lines.Length; i++)
                ParseHeaderLine(lines[i], template);
        }

        private void ParseRequestLine(string line, HttpRequestTemplate template)
        {
            var match = Regex.Match(line, RegexPatterns.RequestLine);
            if (!match.Success) throw new Exception($"请求行格式错误: {line}");

            template.Method = match.Groups[1].Value;
            string urlPart = match.Groups[2].Value;
            template.Url = ProcessUrlVariables(urlPart, template);
        }

        private string ProcessUrlVariables(string url, HttpRequestTemplate template)
        {
            var varRegex = new Regex(RegexPatterns.Variable, RegexOptions.Compiled);
            return varRegex.Replace(url, match =>
            {
                string varName = match.Groups["var"].Value;
                string format = match.Groups["format"].Success ? match.Groups["format"].Value : null;
                var expression = new TemplateExpression
                {
                    Id = StringHelper.GenerateUniqueId(),
                    VariableName = varName,
                    VariableType = VariableType.CreateScalar(VariableKind.String),
                    FormatString = format,
                    OriginalText = match.Value,
                    Location = ExpressionLocation.Url
                };
                template.Expressions.Add(expression);
                _vars.RegisterVariable(new Variable(varName, expression.VariableType, VariableSource.Request, format));
                return match.Value;
            });
        }

        private void ParseHeaderLine(string line, HttpRequestTemplate template)
        {
            var match = Regex.Match(line, RegexPatterns.HeaderLine);
            if (!match.Success) return;

            string key = match.Groups[1].Value.Trim();
            string value = match.Groups[2].Value.Trim();
            template.Headers[key] = ProcessHeaderVariables(value, template);
        }

        private string ProcessHeaderVariables(string headerValue, HttpRequestTemplate template)
        {
            var varRegex = new Regex(RegexPatterns.Variable, RegexOptions.Compiled);
            return varRegex.Replace(headerValue, match =>
            {
                string varName = match.Groups["var"].Value;
                string format = match.Groups["format"].Success ? match.Groups["format"].Value : null;
                var expression = new TemplateExpression
                {
                    Id = StringHelper.GenerateUniqueId(),
                    VariableName = varName,
                    VariableType = VariableType.CreateScalar(VariableKind.String),
                    FormatString = format,
                    OriginalText = match.Value,
                    Location = ExpressionLocation.Header
                };
                template.Expressions.Add(expression);
                _vars.RegisterVariable(new Variable(varName, expression.VariableType, VariableSource.Request, format));
                return match.Value;
            });
        }

        // Added: Body parsing now records placeholder pointers and array descriptors.
        private void ParseBodySection(string bodySection, HttpRequestTemplate template)
        {
            string processed = ProcessBodyVariables(bodySection, template);
            template.BodyTemplate = processed;
            if (string.IsNullOrWhiteSpace(processed)) return;

            try
            {
                var rootNode = _jsonProcessor.ParseJson(processed);
                AnalyzeBodyPlaceholders(rootNode, template);
            }
            catch (Exception ex)
            {
                Logger.LogError("RequestTemplateParser: ParseBodySection failed", ex);
                throw new Exception($"请求Body JSON解析失败: {ex.Message}");
            }
        }

        // Added: Replaces every body placeholder with a quoted marker so JSON parsing succeeds.
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
                var varType = isArray
                    ? VariableType.CreateArray(VariableType.CreateScalar(elemKindType))
                    : VariableType.CreateScalar(elemKindType);

                var expression = new TemplateExpression
                {
                    Id = StringHelper.GenerateUniqueId(),
                    VariableName = varName,
                    VariableType = varType,
                    FormatString = format,
                    OriginalText = match.Value,
                    Location = ExpressionLocation.Body
                };
                template.Expressions.Add(expression);
                _vars.RegisterVariable(new Variable(varName, varType, VariableSource.Request, format));

                // Added: Always quote body placeholders so JsonNode.Parse succeeds regardless of declared type.
                return "\"" + StringHelper.CreatePlaceholder(expression.Id) + "\"";
            });
        }

        // Added: Traverse the parsed JsonNode to distinguish array slots from scalar placeholders.
        private void AnalyzeBodyPlaceholders(JsonNode rootNode, HttpRequestTemplate template)
        {
            var bodyExpressions = template.Expressions
                .Where(e => e.Location == ExpressionLocation.Body)
                .ToDictionary(e => e.Id, e => e);

            _jsonProcessor.TraverseJson(rootNode, "", (path, value) =>
            {
                if (value is not string placeholder || !StringHelper.IsPlaceholder(placeholder)) return;
                var id = StringHelper.ExtractIdFromPlaceholder(placeholder);
                if (id == null || !bodyExpressions.TryGetValue(id, out var expression)) return;

                var projection = TemplateArrayHelper.TryDetectArrayProjection(rootNode, path);
                if (projection != null)
                {
                    // Added: Promote variables found under array elements so request builder knows to broadcast them.
                    expression.VariableType = PromoteToArrayType(expression.VariableType);
                    var descriptor = GetOrCreateArrayDescriptor(template, projection.CollectionPointer, rootNode);
                    descriptor.Slots.Add(new ArrayElementSlot
                    {
                        RelativePointer = projection.ElementPointer,
                        Expression = expression
                    });
                    _vars.RegisterVariable(new Variable(expression.VariableName, expression.VariableType, VariableSource.Request, expression.FormatString));
                }
                else
                {
                    template.BodyPlaceholders.Add(new RequestBodyPlaceholder
                    {
                        Pointer = path,
                        Expression = expression
                    });
                }
            });
        }

        // Added: Ensure every array block has a descriptor plus a sample element for cloning.
        private ArrayTemplateDescriptor GetOrCreateArrayDescriptor(HttpRequestTemplate template, string collectionPointer, JsonNode rootNode)
        {
            var descriptor = template.ArrayTemplates.FirstOrDefault(d => d.CollectionPointer == collectionPointer);
            if (descriptor != null) return descriptor;

            var collectionNode = _jsonProcessor.GetNodeByPointer(rootNode, collectionPointer) as JsonArray;
            if (collectionNode == null || collectionNode.Count == 0)
                throw new Exception($"数组 {collectionPointer} 需要至少包含一个示例元素");

            descriptor = new ArrayTemplateDescriptor
            {
                CollectionPointer = collectionPointer,
                ElementPrototype = collectionNode[0]?.DeepClone()
            };
            template.ArrayTemplates.Add(descriptor);
            return descriptor;
        }

        private VariableKind ParseVariableType(string typeStr, out bool isArray)
        {
            isArray = false;
            if (string.IsNullOrEmpty(typeStr)) return VariableKind.String;

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

            return s.ToLower() switch
            {
                "bool" => VariableKind.Bool,
                "int" => VariableKind.Int,
                "number" => VariableKind.Int,
                "float" => VariableKind.Float,
                "string" => VariableKind.String,
                "datetime" => VariableKind.DateTime,
                _ => throw new Exception($"不支持的数据类型: {typeStr}")
            };
        }

        // Added: Helper to bump scalar declarations up to Array<T> when模板位置要求如此.
        private VariableType PromoteToArrayType(VariableType type)
        {
            if (type != null && type.IsArray) return type;
            var elementType = type ?? VariableType.CreateScalar(VariableKind.String);
            return VariableType.CreateArray(elementType);
        }
    }
}
