# SuccessCriteria 表达式数组访问改造说明

> 目标：在成功判定表达式里直接编写 `Items.Count > 0` 或 `Items[0] = "foo"` 这样的语句，从已有数组变量中推断数量或取指定下标的元素。

## 1. Tokenizer 支持新的符号

文件：`Core/Parsers/SuccessCriteriaParser.cs`

- 在 `Tokenize` 阶段新增 `.`、`[`、`]` 三种符号的 Token，并将小数解析拆出来，避免与变量访问语法冲突。
- 变量名块去掉了 `.`，这样 `Orders.Count` 会被识别为「变量 `Orders` + Dot + 变量 `Count`」，为后续访问器解析做准备。

## 2. VariableExpression 抽象

文件：`Core/Models/ConditionNodes.cs`

- `ConditionNode` 现在只是一棵纯 AST，`ComparisonNode`、`BooleanVariableNode` 分别引用 `VariableExpression`。
- `VariableExpression` + `AccessorSegment` 统一描述「基础变量 + 一串访问步骤」。
- AST 不再承载执行逻辑，方便做缓存与调试。

## 3. Parser 生成 VariableExpression

文件：`Core/Parsers/SuccessCriteriaParser.cs`

- `ParseVariableExpression()` 会在变量名后持续读取 `.属性` 与 `[数字]`，写入 AccessorSegment 列表。
- `ParseComparisonExpression()` 直接把 `VariableExpression` 挂在 `ComparisonNode` 上，剩余流程（操作符、常量值）保持不变。

## 4. 求值流程

文件：`Core/Evaluators/*.cs`

- `VariableExpressionEvaluator` 根据访问器链提取真实值，支持 `Count` 和 `[index]`，失败时返回具体错误。
- `ConditionEvaluator` 递归遍历 AST，调用 `VariableExpressionEvaluator` 取值，再执行比较/逻辑组合。HttpTestService 只和这个类交互。

## 5. 使用示例

| 表达式 | 说明 |
| ------ | ---- |
| `preferences.Count > 1` | Count 属性会返回整数，可与数字比较。 |
| `preferences[0] = "language"` | 对数组/List 取第 0 个元素并比较字符串。 |
| `preferences[1].Count` | 语法允许嵌套，但当前仅内置 `Count`/`[index]` 两种操作。 |
| `!(preferences.Count = 0)` | 现在支持 `!` 取反，可以把任意表达式反转。 |
| `preferences[0] != "admin"` | `!=` 用于判断“不等于”。 |

若访问器无法应用（例如变量不是数组却写了 `.Count`，或下标越界），`TryResolve` 会返回 `false`，整个条件判定会视为失败，避免抛异常影响主流程。

## 6. 重构后的整体架构

- **Tokenizer (`SuccessCriteriaTokenizer`)**：只负责把表达式切成 Token，语法扩展时先改这里。
- **Parser (`SuccessCriteriaParser`)**：根据 Token 生成 AST（`ConditionNode` 树），节点内部不夹杂执行逻辑。
- **VariableExpressionEvaluator**：专门解析 VariableExpression，支持 `.Count`、`[index]`，失败会返回详细错误。
- **ConditionEvaluator**：递归遍历 AST，调用 VariableExpressionEvaluator 取值，再走比较逻辑。HttpTestService 只和它打交道。

这样分层后，每块职责非常清晰：想新增访问器就改 `VariableExpressionEvaluator`，想新增运算符就扩展 `ConditionEvaluator` + `Parser`，把“看不懂的长文件”拆成了几块小模块。
