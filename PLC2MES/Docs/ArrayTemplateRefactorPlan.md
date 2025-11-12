# JSON 模板数组化设计（最新版）

> 目标：**解析阶段一次性确定变量类型与数组结构**，运行时只依赖解析产物即可完成请求生成与响应解析；同时保持模型精简、行为可预测，避免额外的“类型提升”补丁。

---

## 1. 核心原则

1. **占位符一律加引号**：模板中的 `@Type(Name[:format])` 全部替换成 `"${id}$"`，保证 body 100% 合法 JSON；数组/对象最终值在运行阶段再覆写这段 placeholder。
2. **位置决定类型**：解析 `JsonNode` 后，通过 JSON Pointer 判断占位符是否位于数组元素内；只要路径上出现数组索引，就把变量类型升级为 `Array<元素类型>`，并记录数组元数据。
3. **一次解析，多端复用**：解析阶段生成的元数据（下述模型）同时供请求生成和响应解析使用，运行时不再猜测结构。
4. **限制清晰**：若数组缺少示例元素，或数组元素中再嵌套数组，立即在解析阶段报错，避免运行时意外。

---

## 2. 解析阶段产物

### 2.1 请求模板

| 模型 | 用途 | 关键字段 |
| --- | --- | --- |
| `TemplateExpression` | 记录变量定义（变量名、类型、格式、位置）。解析阶段一旦确定数组类型，就直接更新这里以及变量管理器。 | `VariableType` 即最终类型；`ElementType` 只是辅助属性 |
| `RequestBodyPlaceholder` | 非数组占位符的 JSON Pointer. | `Pointer`, `Expression` |
| `ArrayTemplateDescriptor` | 描述 body 中的某个数组块。 | `CollectionPointer`, `ElementPrototype`, `List<ArrayElementSlot>` |
| `ArrayElementSlot` | 数组元素内部的单个占位符。 | `RelativePointer`, `ExpressionId/Expression` |

**解析流程**：
1. body → placeholder 替换 → `JsonNode.Parse`.
2. DFS 遍历树，遇到 `"${id}$"` 时：
   - 若路径在数组元素内：升级变量类型，定位 `CollectionPointer`，取首个元素作为 `ElementPrototype`，记录 `ArrayElementSlot(RelativePointer)`。
   - 否则：生成 `RequestBodyPlaceholder`。
3. 任何数组元素内部再次出现数组 → 抛出 “暂不支持数组嵌套”。

### 2.2 响应模板

| 模型 | 用途 | 关键字段 |
| --- | --- | --- |
| `ResponseHeaderMapping` | header capture group → 变量 | `HeaderName`, `RegexPattern`, `GroupIndex`, `VariableType` |
| `ResponseBodyMapping` | body placeholder → 变量 | `Pointer`（标量）或 `Projection`（数组） |
| `ArrayProjectionInfo` | 描述数组投影 | `CollectionPointer`, `ElementPointer` |

解析思路与请求体相同：根据 pointer 判定是否位于数组元素；若是，则填充 `Projection`，变量类型升级为数组；否则直接存 `Pointer`。

---

## 3. 运行阶段逻辑

### 3.1 请求生成（HttpRequestProcessor）

1. **默认兼容**：若模板没有记录任何数组/指针信息，保持旧的“字符串替换”行为。
2. **结构化生成**：
   - 解析 body 模板为 `JsonNode`（可在模板对象中缓存）。
   - 对每个 `ArrayTemplateDescriptor`：
     1. 收集所有 slot 变量的值，转换为值序列 `List<object>`（标量自动广播，数组长度需一致）。
     2. 克隆 `ElementPrototype`，对每个 slot 使用 `JsonProcessor.SetNodeByPointer` 写入对应索引值；若 `RelativePointer` 为 `/`，直接用值替换整个元素。
     3. 生成 N 个元素后，替换 `CollectionPointer` 对应的 `JsonArray`。
   - 对所有 `RequestBodyPlaceholder`：把变量值（或默认值）转成 `JsonNode`，写入对应 pointer；`Pointer="/"` 的情况直接替换 root。
   - `rootNode.ToJsonString()` 作为最终 body。

### 3.2 响应解析（HttpResponseProcessor）

1. **Header**：遍历 `ResponseHeaderMapping`，用 Regex 捕获组填变量。
2. **Body 标量**：`Pointer` 非空的 mapping 直接 `JsonProcessor.GetValueByPointer` → `TypeConverter`.
3. **Body 数组**：`Projection` 非空时，定位 `CollectionPointer`，遍历 `JsonArray`，基于 `ElementPointer` 聚合值并赋给变量。

---

## 4. 值序列与错误策略

| 场景 | 行为 |
| --- | --- |
| 变量值是数组/集合 | 直接展开。 |
| 变量声明为数组但值是单个标量 | 视为长度 1，可广播。 |
| 多个数组变量长度不同 | 构建请求时抛错，提示变量名。 |
| 变量缺失或空集合 | 使用元素类型默认值；若需要广播则复制默认值。 |
| 响应中缺少数组节点 | 将变量重置为默认值，记录日志。 |
| Body 解析失败 / JSON 非法 | 模板解析阶段即报错；响应阶段遇到非 JSON body 则跳过 body 映射并记录 info。 |

---

## 5. 为什么需要 ArrayElementSlot？

- Slot 只保存两件事：**元素内的相对路径**和**对应的表达式**。  
- 有了它，生成阶段无需再次遍历元素寻找 `"${id}$"`，也不必重新计算 pointer；直接根据 slot 写入值即可。  
- 如果取消 slot，就必须在运行阶段对每个元素做一次 DFS+匹配，占用 CPU 且代码更复杂；本方案选择在解析阶段一次性计算，运行时只做简单替换。

---

## 6. 可选优化

1. 在模板对象中缓存 `JsonNode` 版本的 body，生成请求时直接 `DeepClone()`，避免重复 parse。
2. 提供 `TypeConverter.ToJsonNode`，直接生成 `JsonValue/JsonObject/JsonArray`，不再依赖“字符串 → JsonNode.Parse”的中间步骤。
3. 后续若要支持数组嵌套，可在 `TemplateArrayHelper` 中改成递归 descriptor；当前版本为了可控，先限制一层。

---

如对以上方案仍有疑问（例如希望进一步合并模型、放宽嵌套限制等），请指出，我们可以再讨论调整。***
