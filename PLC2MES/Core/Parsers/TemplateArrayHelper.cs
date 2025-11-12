using System;
using System.Linq;
using System.Text.Json.Nodes;
using PLC2MES.Core.Models;
using PLC2MES.Core.Processors;

namespace PLC2MES.Core.Parsers
{
    // Added: Helper to detect array projections and validate unsupported nested-array scenarios.
    internal static class TemplateArrayHelper
    {
        public static ArrayProjectionInfo TryDetectArrayProjection(JsonNode rootNode, string pointer)
        {
            if (rootNode == null || string.IsNullOrEmpty(pointer)) return null;

            var segments = JsonProcessor.SplitPointerSegments(pointer);
            if (segments.Length == 0) return null;

            JsonNode current = rootNode;
            string collectionPointer = null;
            string elementPointer = null;
            int arrayDepth = 0;

            for (int i = 0; i < segments.Length; i++)
            {
                if (current is JsonArray array)
                {
                    arrayDepth++;
                    if (arrayDepth > 1)
                        throw new Exception($"暂不支持数组中嵌套数组: {pointer}");

                    if (!int.TryParse(segments[i], out var index) || index < 0 || index >= array.Count)
                        throw new Exception($"数组段 {segments[i]} 无效，无法解析指针 {pointer}");

                    // Added: Capture the pointer to the array and the remaining path inside the element.
                    collectionPointer = JsonProcessor.BuildPointerFromSegments(segments.Take(i), true, true);
                    elementPointer = JsonProcessor.BuildPointerFromSegments(segments.Skip(i + 1), true, false);

                    current = array[index];
                    continue;
                }

                if (current is JsonObject obj)
                {
                    if (!obj.TryGetPropertyValue(segments[i], out current))
                    {
                        current = null;
                    }
                }
            }

            if (collectionPointer == null) return null;
            if (string.IsNullOrEmpty(elementPointer)) elementPointer = "/";

            // Added: Return the projection info so callers can aggregate values later.
            return new ArrayProjectionInfo
            {
                CollectionPointer = collectionPointer,
                ElementPointer = elementPointer
            };
        }
    }
}
