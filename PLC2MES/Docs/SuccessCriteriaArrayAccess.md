# SuccessCriteria 表达式数组访问改造说明

> 目标：在成功判定表达式里直接编写 `Items.Count > 0` 或 `Items[0] = "foo"` 这样的语句，从已有数组变量中推断数量或取指定下标的元素。

## 1. Tokenizer 支持新的符号

文件：`Core/Parsers/SuccessCriteriaParser.cs`

- 在 `Tokenize` 阶段新增 `.`、`[`、`]` 三种符号的 Token，并将小数解析拆出来，避免与变量访问语法冲突。
- 变量名块去掉了 `.`，这样 `Orders.Count` 会被识别为「变量 `Orders` + Dot + 变量 `Count`」，为后续访问器解析做准备。

## 2. VariableAccessor 抽象

文件：`Core/Models/ConditionNodes.cs`

- 新增 `VariableAccessor` + `AccessorSegment` 结构体，统一描述「基础变量 + 一串访问步骤」。
- `ComparisonNode`、`BooleanVariableNode` 不再只保存 `VariableName`，而是保存完整的访问器；`ToString()` 也同步更新，方便调试。
- 新建 `VariableAccessorResolver`：
  - 负责按照访问步骤去变量字典里取值；
  - 目前支持 `Count` 属性（对 `List<T>`、数组、`IEnumerable`、`JsonArray` 统计长度）与 `[index]`（下标访问）；
  - 解析过程中会动态更新 `VariableType`，例如 `Orders.Count` 的类型返回 Int，`Orders[0]` 的类型为元素类型，方便比较逻辑继续复用原有的 `Equal`/`GreaterThan` 等函数。

## 3. Parser 生成访问器

文件：`Core/Parsers/SuccessCriteriaParser.cs`

- 增加 `ParseVariableExpression()`：遇到变量时先记录基础名，然后循环读取 `.属性` 和 `[数字]`，分别生成 `AccessorSegmentKind.Property` 或 `Index`。
- `ParseComparisonExpression()` 调整为直接使用 `VariableAccessor`，剩余流程（解析操作符、常量值）保持不变。

## 4. 求值流程

文件：`Core/Models/ConditionNodes.cs`

- `ComparisonNode.Evaluate()` 在比较前调用 `VariableAccessorResolver.TryResolve()`，拿到真实值与推断类型，再走原来的比较逻辑。
- `BooleanVariableNode` 同理，只是只在最终类型为 Bool 时返回结果，保持与先前行为一致。

## 5. 使用示例

| 表达式 | 说明 |
| ------ | ---- |
| `preferences.Count > 1` | Count 属性会返回整数，可与数字比较。 |
| `preferences[0] = "language"` | 对数组/List 取第 0 个元素并比较字符串。 |
| `preferences[1].Count` | 语法允许嵌套，但当前仅内置 `Count`/`[index]` 两种操作。 |

若访问器无法应用（例如变量不是数组却写了 `.Count`，或下标越界），`TryResolve` 会返回 `false`，整个条件判定会视为失败，避免抛异常影响主流程。
