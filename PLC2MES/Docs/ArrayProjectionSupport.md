# 响应模板数组投影改造说明（新手友好版）

> 目标：在响应模板中允许像 `"preferences": [{ "category": @Array<string>(Category) }]` 这样的写法，把数组元素里的字段收集成数组变量，例如 `Category = ["language", "timezone"]`。这样写脚本时就不用自己循环 JSON，只要在模板里声明一次即可。

## 0. 改造总览

1. **模板阶段**（`ResponseTemplateParser`）：找到所有 `@Array<T>(Var)` 占位符，并判断它们是不是出现在数组元素内部。如果是，将该变量标记为“数组投影映射”。
2. **结构描述**（`ResponseMapping`）：为“数组投影映射”额外存 `CollectionPointer`（数组根路径）和 `ElementRelativePointer`（元素内部路径）。
3. **运行阶段**（`HttpResponseProcessor`）：按新的指针信息找到数组，遍历元素，把每个元素里对应字段的值依次塞进变量。
4. **指针辅助**（`JsonProcessor`）：新增很多帮助方法，方便解析阶段与运行阶段都能安全地从 JSON 节点里取数据。

下面逐步说明每个模块的具体改动及如何应用。

## 1. ResponseMapping 扩展

文件：`Core/Models/ResponseMapping.cs`

- 新增两个可选字段，用来描述“数组投影”：
  - `CollectionPointer`：字符串，指向包含目标元素的数组节点（JSON Pointer）。例如 `/preferences`。
  - `ElementRelativePointer`：字符串，描述数组元素内部的相对路径（以 `/` 开头，可为空，空表示取整个元素）。例如 `/category`。
- 分析逻辑：
  - 如果某个数组变量的 JsonPointer 是 `/preferences/0/category`，那么 `CollectionPointer` = `/preferences`，`ElementRelativePointer` = `/category`。
  - 这些字段只有在“投影”场景会被赋值；普通场景仍沿用 `JsonPointer`。
  - 解析完毕后，`HttpResponseProcessor` 就能根据 `CollectionPointer` 知道该去哪个数组抓值，再用 `ElementRelativePointer` 知道要拿元素里哪个字段。

> JSON Pointer 提示：`/preferences/0/category` 的含义是「根对象里的 `preferences` 数组 -> 下标 0 -> 里边的 `category` 字段」。

## 2. JsonProcessor 能力增强

文件：`Core/Processors/JsonProcessor.cs`

为了实现“数组投影”，需要在任意节点上做二次取值，因此扩展了以下工具方法：

| 方法 | 作用 | 举例 |
| ---- | ---- | ---- |
| `TryGetNodeByPointer(JsonNode root, string pointer, out JsonNode node)` | 根据 JSON Pointer 获取原始 `JsonNode`（而不是直接转成 string），便于后续继续深入。 | 传入 `/preferences`，返回整个数组节点。 |
| `GetNodeByPointer(...)` | `TryGetNodeByPointer` 的便捷版，找不到时返回 `null`。 | |
| `GetValueFromNode(JsonNode root, string relativePointer)` | 以某个节点为根，继续用相对路径取值。 | 先拿到 `preferences` 数组，再对每个元素取 `/category`。 |
| `SplitPointerSegments(string pointer)` | 把 `/preferences/0/category` 拆成 `["preferences","0","category"]`，方便判断哪一段是数组索引。 | |
| `BuildPointerFromSegments(IEnumerable<string> segments, bool includeLeadingSlash, bool emptyResultAsSlash)` | 反向操作：把段重新拼成合法 Pointer，并自动处理转义。 | |
| `EscapePointerSegment` / `UnescapePointerSegment` | 处理 JSON Pointer 中 `~`、`/` 的编码。 | |

这些函数基本都是“字符串分割 + 判空 + 访问节点”，对于新手来说也容易理解。只要记住：**指针其实就是一条路径**，我们手动拆它是为了找到路径上的数组索引位置。

## 3. ResponseTemplateParser 调整

文件：`Core/Parsers/ResponseTemplateParser.cs`

1. **占位符统一包裹引号**：不管是 `@String` 还是 `@Array`，替换成占位符时都会写成 `"${ID}$"`。这样可以保证模板本身 100% 是合法 JSON，后续解析不会再因为裸数组值而失败。真正的类型信息仍存在 `VariableType` 里。
2. **识别数组投影**：
   - 在 `TraverseJson` 回调中，如果当前占位符对应的变量是数组，就调用 `TryConfigureArrayProjectionMapping`。
   - 该方法做的事情：
     1. 用 `SplitPointerSegments` 拆分占位符路径；
     2. 从后往前找数组索引（判断段里是否是数字）；
     3. 一旦找到，就把索引前面的段拼成 `CollectionPointer`，剩余的段拼成 `ElementRelativePointer`；
     4. 再确认 `CollectionPointer` 真的指向 `JsonArray`，避免误判；
     5. 如果全部成立，就把 `mapping.JsonPointer` 置空，表示后续不走旧逻辑。
   - 如果占位符本身就是“整个数组”（比如直接写在 `roles` 上），那路径里没有数值段，自然不会触发上述逻辑，仍使用旧行为。

这样处理后，模板层面无需新增语法，只是内部多判断了一步。

## 4. HttpResponseProcessor 行为更新

文件：`Core/Processors/HttpResponseProcessor.cs`

1. `ProcessBody` 在循环 `bodyMappings` 时先检查 `CollectionPointer` 是否有值：
   - 有值 → 说明这是“数组投影映射”，调用 `ProcessCollectionMapping`；
   - 无值 → 走旧逻辑，直接按 `JsonPointer` 取单个值。
2. `ProcessCollectionMapping` 的步骤：
   1. `GetNodeByPointer(rootNode, mapping.CollectionPointer)` 取到数组节点；
   2. 如果不是数组或者为空，就为变量写默认值；
   3. 否则遍历该 `JsonArray`：
      - 对每个元素调用 `_jsonProcessor.GetValueFromNode(element, mapping.ElementRelativePointer)` 拿到单个字段；
      - 用 `TypeConverter.ConvertFromJson(rawValue, elementType)` 转成变量元素类型，例如 string/int；
      - 将结果保存到 `List<object>`；
   4. 遍历结束，把 `List<object>` 交给 `SetOrRegisterVariable`，变量自然就是数组值。

> 注意：为了兼容“整个元素”场景，`ElementRelativePointer` 可以为空或 `/`，此时 `GetValueFromNode` 会直接返回整个元素对象。

## 5. 模板书写与调试建议

1. **单字段投影**（最常见）：  
   ```json
   "preferences": [
     {
       "category": @Array<string>(Category),
       "value": @Array<string>(PreferenceValue)
     }
   ]
   ```  
   解析结果：`Category = ["language","timezone"]`，`PreferenceValue = ["zh-CN","Asia/Shanghai"]`。

2. **整个数组仍按旧写法**：  
   ```json
   "roles": @Array<string>(Roles)
   ```  
   因为没出现在子字段上，所以 `Roles` 变量直接得到整个数组。

3. **嵌套数组**：  
   模板里如果出现 `orders` → `items` → `sku`，写成 `@Array<string>(Sku)`，系统也能识别，因为它会一直向上找到最近的数组（`items`），自动计算指针。

4. **调试技巧**：
   - 可以在模板解析完后打印 `HttpResponseTemplate.Mappings`，确认 `CollectionPointer` 是否按预期生成。
   - 如果变量一直是空列表，优先检查 JSON Pointer 是否写错（可借助 `SplitPointerSegments` 输出看看）。
   - 若需要自定义默认值，可以在变量管理器里给变量设置默认，这样就算 JSON 里缺字段也不会是 null。

完成以上改造后，就能在模板层面解决“数组元素字段聚合”的痛点，真正把解析逻辑封装在框架里，业务脚本只需要声明变量即可。
