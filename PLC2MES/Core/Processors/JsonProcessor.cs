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
            if (!TryGetNodeByPointer(root, jsonPointer, out var node)) return null;
            return ConvertJsonNode(node);
        }

        /// <summary>
        /// 返回原始 JsonNode，方便上层继续遍历
        /// </summary>
        public JsonNode GetNodeByPointer(JsonNode root, string jsonPointer)
        {
            TryGetNodeByPointer(root, jsonPointer, out var node);
            return node;
        }

        /// <summary>
        /// 尝试根据 JSON Pointer 获取 JsonNode
        /// </summary>
        public bool TryGetNodeByPointer(JsonNode root, string jsonPointer, out JsonNode node)
        {
            node = null;
            if (root == null) return false;
            if (string.IsNullOrEmpty(jsonPointer) || jsonPointer == "/")
            {
                node = root;
                return true;
            }

            var segments = SplitPointerSegments(jsonPointer);
            JsonNode current = root;

            foreach (var seg in segments)
            {
                if (current is JsonObject obj)
                {
                    // 对象节点：把段当作属性名
                    if (!obj.TryGetPropertyValue(seg, out JsonNode next)) return false;
                    current = next;
                }
                else if (current is JsonArray arr)
                {
                    // 数组节点：段需要能转换成索引
                    if (!int.TryParse(seg, out int idx) || idx < 0 || idx >= arr.Count) return false;
                    current = arr[idx];
                }
                else
                {
                    return false;
                }
            }

            node = current;
            return true;
        }

        /// <summary>
        /// 以某个节点作为根节点，继续使用相对 Pointer 取值
        /// </summary>
        public object GetValueFromNode(JsonNode root, string relativePointer)
        {
            if (root == null) return null;
            if (string.IsNullOrEmpty(relativePointer) || relativePointer == "/") return ConvertJsonNode(root);

            // relativePointer 可能是 "category" 或 "/category"，统一转换成绝对指针再复用 TryGetNodeByPointer
            string normalized = relativePointer.StartsWith("/") ? relativePointer : "/" + relativePointer;
            return TryGetNodeByPointer(root, normalized, out var node) ? ConvertJsonNode(node) : null;
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
                    string newPath = currentPath + "/" + EscapePointerSegment(kvp.Key);
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
        /// 拆分 JSON Pointer 为解码后的段
        /// </summary>
        public static string[] SplitPointerSegments(string jsonPointer)
        {
            if (string.IsNullOrEmpty(jsonPointer)) return Array.Empty<string>();
            var segments = jsonPointer.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0) return Array.Empty<string>();

            var result = new string[segments.Length];
            for (int i = 0; i < segments.Length; i++)
            {
                // Pointer 中的 "~1" 表示 "/"，"~0" 表示 "~"，这里需要反转义成原字符
                result[i] = UnescapePointerSegment(segments[i]);
            }

            return result;
        }

        /// <summary>
        /// 根据段构建 JSON Pointer，可控制前缀
        /// </summary>
        public static string BuildPointerFromSegments(IEnumerable<string> segments, bool includeLeadingSlash = true, bool emptyResultAsSlash = true)
        {
            if (segments == null)
            {
                return includeLeadingSlash
                    ? (emptyResultAsSlash ? "/" : string.Empty)
                    : string.Empty;
            }

            var list = new List<string>();
            foreach (var seg in segments)
            {
                if (string.IsNullOrEmpty(seg)) continue;
                // 构建指针时需要做转义，防止属性名里本身带 "/" 或 "~"
                list.Add(EscapePointerSegment(seg));
            }

            if (list.Count == 0)
            {
                return includeLeadingSlash
                    ? (emptyResultAsSlash ? "/" : string.Empty)
                    : string.Empty;
            }

            string joined = string.Join("/", list);
            return includeLeadingSlash ? "/" + joined : joined;
        }

        public static string EscapePointerSegment(string segment)
        {
            if (segment == null) return string.Empty;
            return segment.Replace("~", "~0").Replace("/", "~1");
        }

        private static string UnescapePointerSegment(string segment)
        {
            if (segment == null) return string.Empty;
            return segment.Replace("~1", "/").Replace("~0", "~");
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
