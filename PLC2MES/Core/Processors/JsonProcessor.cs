using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using PLC2MES.Utils;

namespace PLC2MES.Core.Processors
{
    public class JsonProcessor
    {
        public JsonProcessor() { }

        /// <summary>
        /// Parse json into JsonNode
        /// </summary>
        public JsonNode ParseJson(string json)
        {
            try
            {
                return JsonNode.Parse(json);
            }
            catch (Exception ex)
            {
                throw new Exception($"JSON解析失败: {ex.Message}");
            }
        }

        public string ToJson(JsonNode node)
        {
            return node?.ToJsonString(new JsonSerializerOptions { WriteIndented = false }) ?? string.Empty;
        }

        /// <summary>
        /// Get value by JSON Pointer from a JsonNode
        /// </summary>
        public object GetValueByPointer(JsonNode root, string jsonPointer)
        {
            if (root == null) return null;
            if (string.IsNullOrEmpty(jsonPointer) || jsonPointer == "/") return ConvertJsonNode(root);

            string[] segments = jsonPointer.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            JsonNode current = root;

            foreach (var seg in segments)
            {
                string unescaped = seg.Replace("~1", "/").Replace("~0", "~");

                if (current is JsonObject obj)
                {
                    if (!obj.TryGetPropertyValue(unescaped, out JsonNode next))
                        return null;
                    current = next;
                }
                else if (current is JsonArray arr)
                {
                    if (int.TryParse(unescaped, out int idx) && idx >= 0 && idx < arr.Count)
                    {
                        current = arr[idx];
                    }
                    else
                    {
                        return null;
                    }
                }
                else
                {
                    return null;
                }
            }

            return ConvertJsonNode(current);
        }

        /// <summary>
        /// Traverse JSON tree and invoke callback for each node
        /// </summary>
        public void TraverseJson(JsonNode node, string currentPath, Action<string, object> callback)
        {
            if (node == null) return;

            if (node is JsonObject obj)
            {
                foreach (var kvp in obj)
                {
                    string newPath = currentPath + "/" + EscapeJsonPointer(kvp.Key);
                    if (kvp.Value is JsonValue)
                    {
                        callback(newPath, ConvertJsonNode(kvp.Value));
                    }
                    else
                    {
                        callback(newPath, kvp.Value);
                    }

                    TraverseJson(kvp.Value, newPath, callback);
                }
            }
            else if (node is JsonArray arr)
            {
                for (int i = 0; i < arr.Count; i++)
                {
                    string newPath = currentPath + "/" + i;
                    var item = arr[i];
                    if (item is JsonValue)
                        callback(newPath, ConvertJsonNode(item));
                    else
                        callback(newPath, item);

                    TraverseJson(item, newPath, callback);
                }
            }
            else if (node is JsonValue val)
            {
                callback(currentPath, ConvertJsonNode(val));
            }
        }

        private string EscapeJsonPointer(string segment)
        {
            return segment.Replace("~", "~0").Replace("/", "~1");
        }

        private object ConvertJsonNode(JsonNode node)
        {
            if (node == null) return null;
            if (node is JsonValue val)
            {
                try
                {
                    var element = val.GetValue<object>();
                    return element;
                }
                catch
                {
                    // fallback to string
                    return val.ToJsonString();
                }
            }

            // For objects/arrays return their string representation
            return node.ToJsonString();
        }

        /// <summary>
        /// Replace placeholders of form "${ID}$" (with quotes) with provided json values (values already JSON formatted)
        /// </summary>
        public string ReplacePlaceholders(string jsonTemplate, Dictionary<string, string> replacements)
        {
            string result = jsonTemplate;
            foreach (var kv in replacements)
            {
                string placeholder = StringHelper.CreatePlaceholder(kv.Key);
                // replace quoted placeholder first (scalar placeholders)
                string quoted = "\"" + placeholder + "\"";
                result = result.Replace(quoted, kv.Value);
                // replace unquoted placeholder (array placeholders or raw replacements)
                result = result.Replace(placeholder, kv.Value);
            }
            return result;
        }

        public string FormatJson(string json)
        {
            try
            {
                var node = ParseJson(json);
                if (node == null) return json;
                return node.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            }
            catch
            {
                return json;
            }
        }
    }
} 