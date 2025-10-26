

我要开发一个软件项目，在没有网的电脑上使用C#加winform进行开发，除了官方.net的库，无法使用需要nuget下载的库，项目背景如下： 我们公司是做非标自动化设备，众所周知，PLC需要与MES进行通信，通过Http协议，为了方便PLC工作人员发送http报文，并接受response，提取数据，提前说明，我们只考虑以json格式为体发送数据，决定开发一款软件。 软件功能如下： 需要plc人员手动写我规定好的格式的类http报文，格式大致如下： `Post /api/mes/start?stationId=@(StationID:d)&whatever=@(Test) User-Agent: BYD_PLC2MES_Xie_En Content-Type: appreciate/json Cookies: UserAuth=@(AuthCookie) Content-Type: application/json { "start_time": @String(CurrentTime:yyyy-MM-dd HH:mm:ss.fff), "sn": @String(SerialNo), "count": @Number(Count), "done": @Bool(Done) }` 很显然这种格式和真正的http报文不一样，@（）里面放着变量名：格式化，@（）中间写了数据类型，我们目前只需要考虑的是body中的需要写数据类型，只考虑Bool/Int/Float/String/DateTime这几种类型。 当用户写下这个特殊格式的string后，我可以解析该string ，提取其中的变量，并创建变量，后面让用户输入变量的值，解析替换 生成真正的http报文。 同时用户也要写好我规定好的格式的http response来绑定变量的值，格式大致如下： `200 OK Content-Type: application/json { "success": @Bool(Start_Success), "message": @String(Start_Message), "ticket": { "token": @Number(Start_Token), "expires": @String(Start_TokenExpires) } }` 处理过程： 1. 拆分 Header 和 Body 2. 处理 Header 3. 处理body 对每一行，查找 `@\w+\(\w+\)` 给其一个唯一ID，替换为`"${ID}$"`，记录ID和表达式， 替换好就形成了一个正确的Json格式字符串，将其解析到对象树。 如果是response的body： 遍历对象树，对每一个形如 `"${ID}$"` 的字符串节点，记录其路径，生成JSON Pointer，和ID，表达式一起记录。 4. 生成request 克隆对象树，遍历对象树，对每一个形如`"${ID}$"` 的字符串节点，替换为表达式值生成文本。 5. 处理response Json解析得到对象树，对每一个字段，用JSON Pointer 取值，解析对应表达式， 更新变量，未匹配到的使用默认值，对成功标准表达式求值。 用户还要写一个表达式来判断是否pass，也需要我来解析表达式并给出结果。 成功判断标准： `cs $StatusCode=200&&Start_Success&&Start_TokenExpires>"2025-10-24"&&Start_Token like "token22% 只需要考虑这些：&& || = >= <= > < like` 写好表达式后，用户可以点击发送请求，就可以得到结果了！ 可以看出这是一个需要处理很多字符串工作的项目，同时需要考虑的事情也很多，我现在需要你帮我理清项目思路，给出项目架构，项目结构，需要哪些类，类之间的关系，整体开发思路，给我一份详细的开发文档。

## PLC-MES HTTP 测试工具 - 详细开发文档

我来为你梳理这个项目的完整架构和开发思路。这是一个很实用的工具！

## 一、项目架构概览

```
PLCHttpTester/
├── Core/                          # 核心业务逻辑
│   ├── Models/                    # 数据模型
│   ├── Parsers/                   # 解析器
│   ├── Evaluators/                # 求值器
│   ├── Processors/                # 处理器
│   └── Services/                  # 服务层
├── UI/                            # WinForm 界面
└── Utils/                         # 工具类
```

## 二、核心类设计

### 2.1 数据模型层 (Models)

csharp

```
// 变量定义
public class Variable
{
    public string Name { get; set; }                    // 变量名
    public VariableType Type { get; set; }              // 数据类型
    public object Value { get; set; }                   // 变量值
    public string FormatString { get; set; }            // 格式化字符串（如 yyyy-MM-dd）
    public VariableSource Source { get; set; }          // 来源（Request/Response）
}

public enum VariableType
{
    Bool, Int, Float, String, DateTime
}

public enum VariableSource
{
    Request,    // 请求变量（需要用户输入）
    Response    // 响应变量（从响应中提取）
}

// HTTP 请求模板
public class HttpRequestTemplate
{
    public string Method { get; set; }                  // GET/POST/PUT/DELETE
    public string Url { get; set; }                     // URL 路径
    public Dictionary<string, string> Headers { get; set; }
    public string BodyTemplate { get; set; }            // Body 模板字符串
    public List<TemplateExpression> Expressions { get; set; }  // 所有表达式
}

// HTTP 响应模板
public class HttpResponseTemplate
{
    public int? ExpectedStatusCode { get; set; }
    public Dictionary<string, string> Headers { get; set; }
    public string BodyTemplate { get; set; }
    public List<ResponseMapping> Mappings { get; set; }  // JSON路径到变量的映射
}

// 模板表达式（@Type(VarName:format)）
public class TemplateExpression
{
    public string Id { get; set; }                      // 唯一标识符 ${ID}$
    public string VariableName { get; set; }            // 变量名
    public VariableType? DataType { get; set; }         // 数据类型（Body中需要）
    public string FormatString { get; set; }            // 格式化字符串
    public string OriginalText { get; set; }            // 原始文本
    public ExpressionLocation Location { get; set; }    // 位置（URL/Header/Body）
}

public enum ExpressionLocation
{
    Url, Header, Body
}

// 响应映射
public class ResponseMapping
{
    public string Id { get; set; }                      // 表达式ID
    public string JsonPointer { get; set; }             // JSON Pointer 路径
    public string VariableName { get; set; }
    public VariableType DataType { get; set; }
}

// 成功判断条件
public class SuccessCriteria
{
    public string Expression { get; set; }              // 原始表达式
    public List<ConditionNode> ParsedConditions { get; set; }
}

// 条件节点（表达式树）
public abstract class ConditionNode
{
    public abstract bool Evaluate(Dictionary<string, Variable> variables);
}

public class LogicalOperatorNode : ConditionNode
{
    public LogicalOperator Operator { get; set; }  // AND / OR
    public ConditionNode Left { get; set; }
    public ConditionNode Right { get; set; }
    
    public override bool Evaluate(Dictionary<string, Variable> variables)
    {
        bool leftResult = Left.Evaluate(variables);
        bool rightResult = Right.Evaluate(variables);
        return Operator == LogicalOperator.And ? leftResult && rightResult : leftResult || rightResult;
    }
}

public class ComparisonNode : ConditionNode
{
    public string VariableName { get; set; }
    public ComparisonOperator Operator { get; set; }  // = > < >= <= like
    public object CompareValue { get; set; }
    
    public override bool Evaluate(Dictionary<string, Variable> variables) 
    {
        // 实现比较逻辑
    }
}

public enum LogicalOperator { And, Or }
public enum ComparisonOperator { Equal, GreaterThan, LessThan, GreaterOrEqual, LessOrEqual, Like }
```

### 2.2 解析器层 (Parsers)

csharp

```
// 请求模板解析器
public class RequestTemplateParser
{
    private int _expressionIdCounter = 0;
    
    public HttpRequestTemplate Parse(string templateText)
    {
        // 1. 按空行分割 header 和 body
        // 2. 解析第一行获取 Method 和 URL
        // 3. 解析 Headers
        // 4. 解析 Body，提取所有 @Type(Var:format) 表达式
        // 5. 替换为 ${ID}$ 占位符
    }
    
    private TemplateExpression ParseExpression(string expressionText, ExpressionLocation location)
    {
        // 正则解析：@(\w+)?\((\w+)(?::([^)]+))?\)
        // 提取类型、变量名、格式化字符串
    }
}

// 响应模板解析器
public class ResponseTemplateParser
{
    public HttpResponseTemplate Parse(string templateText, List<TemplateExpression> requestExpressions)
    {
        // 1. 解析状态码行
        // 2. 解析 Headers
        // 3. 解析 Body，提取表达式
        // 4. 将 Body 替换占位符后解析为 JSON
        // 5. 遍历 JSON 树，记录每个 ${ID}$ 的 JSON Pointer 路径
    }
    
    private string GenerateJsonPointer(List<string> pathSegments)
    {
        // 生成标准 JSON Pointer: /ticket/token
    }
}

// 成功条件解析器
public class SuccessCriteriaParser
{
    public SuccessCriteria Parse(string expression)
    {
        // 1. 词法分析：分割 token（变量名、操作符、值、括号）
        // 2. 语法分析：构建表达式树
        // 3. 处理运算符优先级（&& 优先于 ||）
    }
    
    private List<Token> Tokenize(string expression) { }
    private ConditionNode BuildExpressionTree(List<Token> tokens) { }
}
```

### 2.3 处理器层 (Processors)

csharp

```
// JSON 处理器
public class JsonProcessor
{
    // 使用 .NET 内置的 System.Text.Json 或 Newtonsoft.Json（如果能用）
    // 如果都不能用，需要自己实现简单的 JSON 解析器
    
    public JsonElement ParseToElement(string json) { }
    
    public object GetValueByPointer(JsonElement root, string jsonPointer)
    {
        // 按照 JSON Pointer 规范查找值
        // /ticket/token -> root["ticket"]["token"]
    }
    
    public string ReplaceTokensInJson(string jsonTemplate, Dictionary<string, object> values)
    {
        // 替换所有 ${ID}$ 为实际值
    }
}

// HTTP 请求处理器
public class HttpRequestProcessor
{
    public string BuildRealHttpRequest(
        HttpRequestTemplate template, 
        Dictionary<string, Variable> variables)
    {
        // 1. 替换 URL 中的变量
        // 2. 替换 Headers 中的变量
        // 3. 替换 Body 中的变量，生成真实 JSON
        // 4. 组装完整的 HTTP 请求文本
    }
    
    public async Task<HttpResponseData> SendRequestAsync(
        string url, 
        string method, 
        Dictionary<string, string> headers, 
        string body)
    {
        // 使用 HttpClient 或 HttpWebRequest 发送请求
    }
}

// HTTP 响应处理器
public class HttpResponseProcessor
{
    private JsonProcessor _jsonProcessor;
    
    public void ProcessResponse(
        HttpResponseData response, 
        HttpResponseTemplate template,
        Dictionary<string, Variable> variables)
    {
        // 1. 解析响应 JSON
        // 2. 遍历所有 ResponseMapping
        // 3. 使用 JSON Pointer 提取值
        // 4. 根据数据类型转换并更新变量
        // 5. 未匹配的变量设置默认值
    }
    
    private object ConvertValue(object jsonValue, VariableType targetType)
    {
        // 类型转换逻辑
    }
}
```

### 2.4 服务层 (Services)

csharp

```
// 变量管理服务
public class VariableManager
{
    private Dictionary<string, Variable> _variables = new Dictionary<string, Variable>();
    
    public void RegisterVariable(Variable variable) { }
    public Variable GetVariable(string name) { }
    public void SetVariableValue(string name, object value) { }
    public Dictionary<string, Variable> GetAllVariables() { }
    public List<Variable> GetRequestVariables() { }  // 需要用户输入的变量
    public List<Variable> GetResponseVariables() { } // 从响应提取的变量
    public void Clear() { }
}

// 主要业务服务
public class HttpTestService
{
    private RequestTemplateParser _requestParser;
    private ResponseTemplateParser _responseParser;
    private SuccessCriteriaParser _criteriaParser;
    private HttpRequestProcessor _requestProcessor;
    private HttpResponseProcessor _responseProcessor;
    private VariableManager _variableManager;
    
    public void LoadRequestTemplate(string templateText)
    {
        // 1. 解析请求模板
        // 2. 注册所有请求变量
    }
    
    public void LoadResponseTemplate(string templateText)
    {
        // 1. 解析响应模板
        // 2. 注册所有响应变量
    }
    
    public void LoadSuccessCriteria(string expression) { }
    
    public async Task<TestResult> ExecuteTestAsync()
    {
        // 1. 验证所有请求变量已赋值
        // 2. 构建真实 HTTP 请求
        // 3. 发送请求
        // 4. 处理响应，提取变量
        // 5. 评估成功条件
        // 6. 返回测试结果
    }
}

// 测试结果
public class TestResult
{
    public bool Success { get; set; }
    public int StatusCode { get; set; }
    public string RequestText { get; set; }      // 实际发送的请求
    public string ResponseText { get; set; }     // 收到的响应
    public Dictionary<string, Variable> ExtractedVariables { get; set; }
    public string SuccessCriteriaResult { get; set; }  // 条件评估详情
    public string ErrorMessage { get; set; }
}
```

## 三、UI 设计

### 3.1 主窗体 (MainForm)
```
┌─────────────────────────────────────────────────────────┐
│  PLC-MES HTTP 测试工具                           [_][□][X] │
├─────────────────────────────────────────────────────────┤
│  [文件] [编辑] [工具] [帮助]                              │
├──────────────────────┬──────────────────────────────────┤
│                      │                                  │
│  【请求模板】         │  【变量管理】                     │
│  ┌─────────────────┐│  ┌────────────────────────────┐  │
│  │                 ││  │ 请求变量 (需要输入值)        │  │
│  │  Post /api/...  ││  │ ┌────┬──────┬──────┬──────┐│  │
│  │  Headers...     ││  │ │名称│ 类型 │ 值   │格式  ││  │
│  │  Body...        ││  │ ├────┼──────┼──────┼──────┤│  │
│  │                 ││  │ │... │ ... │ ...  │ ...  ││  │
│  │                 ││  │ └────┴──────┴──────┴──────┘│  │
│  └─────────────────┘│  │                            │  │
│                      │  │ 响应变量 (自动提取)        │  │
│  【响应模板】         │  │ ┌────┬──────┬──────┬──────┐│  │
│  ┌─────────────────┐│  │ │名称│ 类型 │ 值   │路径  ││  │
│  │  200 OK         ││  │ └────┴──────┴──────┴──────┘│  │
│  │  Response body  ││  └────────────────────────────┘  │
│  └─────────────────┘│                                  │
│                      │  【成功条件】                     │
│  【成功判断条件】     │  ┌────────────────────────────┐  │
│  ┌─────────────────┐│  │ $StatusCode=200&&...       │  │
│  │ Expression...   ││  └────────────────────────────┘  │
│  └─────────────────┘│                                  │
│                      │  [解析模板] [发送请求]            │
├──────────────────────┴──────────────────────────────────┤
│  【执行结果】                                             │
│  ┌──────────────────────────────────────────────────┐   │
│  │ 状态: ✓ 成功 / ✗ 失败                              │   │
│  │ 状态码: 200                                        │   │
│  │ 请求: ...                                          │   │
│  │ 响应: ...                                          │   │
│  └──────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────┘
```

### 3.2 主要控件

csharp

```
public partial class MainForm : Form
{
    private HttpTestService _testService;
    private TextBox txtRequestTemplate;
    private TextBox txtResponseTemplate;
    private TextBox txtSuccessCriteria;
    private DataGridView dgvRequestVariables;
    private DataGridView dgvResponseVariables;
    private RichTextBox rtbResult;
    private Button btnParse;
    private Button btnSend;
    
    private void btnParse_Click(object sender, EventArgs e)
    {
        // 1. 解析请求模板
        // 2. 解析响应模板
        // 3. 解析成功条件
        // 4. 刷新变量列表
    }
    
    private async void btnSend_Click(object sender, EventArgs e)
    {
        // 1. 从 DataGridView 读取用户输入的变量值
        // 2. 调用 ExecuteTestAsync
        // 3. 显示结果
    }
}
```

## 四、开发步骤

### 第一阶段：基础框架（1-2天）

1. ✅ 创建项目结构和基础类
2. ✅ 实现数据模型
3. ✅ 实现 VariableManager
4. ✅ 创建主窗体 UI 框架

⠀
### 第二阶段：解析器开发（2-3天）

1. ✅ 实现 RequestTemplateParser
	* 正则表达式提取 @Type(Var:format)
	* URL、Header、Body 分别处理
2. ✅ 实现 ResponseTemplateParser
	* JSON 占位符替换
	* JSON Pointer 路径生成
3. ✅ 实现 SuccessCriteriaParser
	* 表达式词法分析
	* 表达式树构建

⠀
### 第三阶段：处理器开发（2-3天）

1. ✅ 实现 JsonProcessor
	* 使用 System.Text.Json (推荐) 或 JavaScriptSerializer
2. ✅ 实现 HttpRequestProcessor
	* 模板变量替换
	* HttpClient 请求发送
3. ✅ 实现 HttpResponseProcessor
	* JSON 解析和值提取
	* 类型转换

⠀
### 第四阶段：业务逻辑（1-2天）

1. ✅ 实现 HttpTestService 整体流程
2. ✅ 实现表达式求值逻辑
3. ✅ 错误处理和验证

⠀
### 第五阶段：UI 完善（1-2天）

1. ✅ 完善变量管理界面
2. ✅ 实现结果展示
3. ✅ 添加辅助功能（保存/加载模板等）

⠀
### 第六阶段：测试和优化（1天）

1. ✅ 单元测试
2. ✅ 集成测试
3. ✅ 性能优化

⠀
## 五、关键技术点

### 5.1 正则表达式模式

csharp

```
// 请求变量：@(VarName:format) 或 @Type(VarName:format)
string requestPattern = @"@(?:(\w+)\()?(\w+)(?::([^)]+))?\)?";

// URL 中的变量：@(VarName:format)
string urlPattern = @"@\((\w+)(?::([^)]+))?\)";

// Body 中的变量：@Type(VarName:format)
string bodyPattern = @"@(\w+)\((\w+)(?::([^)]+))?\)";

// 成功条件解析
string conditionPattern = @"(\$?\w+)\s*(&&|\|\||=|>=|<=|>|<|like)\s*([""'].*?[""']|\d+\.?\d*|true|false)";
```

### 5.2 JSON Pointer 实现

csharp

```
public object GetValueByPointer(JsonElement root, string pointer)
{
    if (pointer == "/" || string.IsNullOrEmpty(pointer))
        return root;
    
    string[] segments = pointer.Split('/').Skip(1).ToArray();
    JsonElement current = root;
    
    foreach (string segment in segments)
    {
        string unescaped = segment.Replace("~1", "/").Replace("~0", "~");
        
        if (current.ValueKind == JsonValueKind.Object)
        {
            if (!current.TryGetProperty(unescaped, out current))
                return null;
        }
        else if (current.ValueKind == JsonValueKind.Array)
        {
            if (int.TryParse(unescaped, out int index))
                current = current[index];
            else
                return null;
        }
    }
    
    return current;
}
```

### 5.3 类型转换

csharp

```
private object ConvertValue(object jsonValue, VariableType targetType)
{
    switch (targetType)
    {
        case VariableType.Bool:
            return Convert.ToBoolean(jsonValue);
        case VariableType.Int:
            return Convert.ToInt32(jsonValue);
        case VariableType.Float:
            return Convert.ToDouble(jsonValue);
        case VariableType.String:
            return jsonValue.ToString();
        case VariableType.DateTime:
            return DateTime.Parse(jsonValue.ToString());
        default:
            return jsonValue;
    }
}
```

## 六、注意事项

1. **无网络环境限制**
	* 只能使用 .NET Framework 内置库
	* JSON 处理：System.Text.Json (.NET Core 3.0+) 或 System.Web.Script.Serialization.JavaScriptSerializer
	* HTTP 请求：System.Net.HttpClient 或 System.Net.HttpWebRequest
2. **错误处理**
	* 模板格式错误
	* 变量未赋值
	* HTTP 请求失败
	* JSON 解析失败
	* 类型转换错误
3. **用户体验**
	* 实时语法检查
	* 变量自动提示
	* 请求/响应格式化显示
	* 错误提示友好
4. **扩展性**
	* 支持模板保存/加载
	* 支持测试用例管理
	* 支持批量测试
	* 支持测试历史记录

⠀
这个架构清晰、模块化、易于维护。每个类职责单一,便于测试和后续扩展。需要我详细展开某个模块的实现代码吗?

EX

暂时不要你给我实现代码，输出一份markdown格式的文档，内容是详细的开发过程，详细到，先写什么再写什么，整个开发流程，开发过程，的究极详细版开发文档，即使是从没开发过project的小白程序员根据文档都能把project开发下来。

## PLC-MES HTTP 测试工具 - 完整开发指南

> 本文档适用于初级程序员，将手把手指导完成整个项目的开发过程

## 目录

1. [项目准备](https://claude.ai/chat/230baa53-f735-400a-9728-2476c41a0c93#1-%E9%A1%B9%E7%9B%AE%E5%87%86%E5%A4%87)
2. [第一天：创建项目和基础框架](https://claude.ai/chat/230baa53-f735-400a-9728-2476c41a0c93#2-%E7%AC%AC%E4%B8%80%E5%A4%A9%E5%88%9B%E5%BB%BA%E9%A1%B9%E7%9B%AE%E5%92%8C%E5%9F%BA%E7%A1%80%E6%A1%86%E6%9E%B6)
3. [第二天：数据模型和变量管理](https://claude.ai/chat/230baa53-f735-400a-9728-2476c41a0c93#3-%E7%AC%AC%E4%BA%8C%E5%A4%A9%E6%95%B0%E6%8D%AE%E6%A8%A1%E5%9E%8B%E5%92%8C%E5%8F%98%E9%87%8F%E7%AE%A1%E7%90%86)
4. [第三天：请求模板解析器](https://claude.ai/chat/230baa53-f735-400a-9728-2476c41a0c93#4-%E7%AC%AC%E4%B8%89%E5%A4%A9%E8%AF%B7%E6%B1%82%E6%A8%A1%E6%9D%BF%E8%A7%A3%E6%9E%90%E5%99%A8)
5. [第四天：响应模板解析器](https://claude.ai/chat/230baa53-f735-400a-9728-2476c41a0c93#5-%E7%AC%AC%E5%9B%9B%E5%A4%A9%E5%93%8D%E5%BA%94%E6%A8%A1%E6%9D%BF%E8%A7%A3%E6%9E%90%E5%99%A8)
6. [第五天：成功条件解析器](https://claude.ai/chat/230baa53-f735-400a-9728-2476c41a0c93#6-%E7%AC%AC%E4%BA%94%E5%A4%A9%E6%88%90%E5%8A%9F%E6%9D%A1%E4%BB%B6%E8%A7%A3%E6%9E%90%E5%99%A8)
7. [第六天：JSON处理器](https://claude.ai/chat/230baa53-f735-400a-9728-2476c41a0c93#7-%E7%AC%AC%E5%85%AD%E5%A4%A9json%E5%A4%84%E7%90%86%E5%99%A8)
8. [第七天：HTTP请求处理器](https://claude.ai/chat/230baa53-f735-400a-9728-2476c41a0c93#8-%E7%AC%AC%E4%B8%83%E5%A4%A9http%E8%AF%B7%E6%B1%82%E5%A4%84%E7%90%86%E5%99%A8)
9. [第八天：HTTP响应处理器](https://claude.ai/chat/230baa53-f735-400a-9728-2476c41a0c93#9-%E7%AC%AC%E5%85%AB%E5%A4%A9http%E5%93%8D%E5%BA%94%E5%A4%84%E7%90%86%E5%99%A8)
10. [第九天：业务服务层](https://claude.ai/chat/230baa53-f735-400a-9728-2476c41a0c93#10-%E7%AC%AC%E4%B9%9D%E5%A4%A9%E4%B8%9A%E5%8A%A1%E6%9C%8D%E5%8A%A1%E5%B1%82)
11. [第十天：主界面设计](https://claude.ai/chat/230baa53-f735-400a-9728-2476c41a0c93#11-%E7%AC%AC%E5%8D%81%E5%A4%A9%E4%B8%BB%E7%95%8C%E9%9D%A2%E8%AE%BE%E8%AE%A1)
12. [第十一天：界面逻辑实现](https://claude.ai/chat/230baa53-f735-400a-9728-2476c41a0c93#12-%E7%AC%AC%E5%8D%81%E4%B8%80%E5%A4%A9%E7%95%8C%E9%9D%A2%E9%80%BB%E8%BE%91%E5%AE%9E%E7%8E%B0)
13. [第十二天：测试和优化](https://claude.ai/chat/230baa53-f735-400a-9728-2476c41a0c93#13-%E7%AC%AC%E5%8D%81%E4%BA%8C%E5%A4%A9%E6%B5%8B%E8%AF%95%E5%92%8C%E4%BC%98%E5%8C%96)

⠀
---

## 1. 项目准备

### 1.1 环境检查

确保你的电脑已安装：

* Visual Studio 2017 或更高版本
* .NET Framework 4.5 或更高版本
### 1.2 知识储备

开发前需要了解的基础知识：

* C# 基本语法
* 正则表达式基础
* HTTP协议基础
* JSON格式
* WinForm 基础
### 1.3 项目规划

**预计开发时间**：12个工作日
**难度等级**：中等
**核心挑战**：字符串解析、表达式求值、JSON处理

---

## 2. 第一天：创建项目和基础框架

### 2.1 创建解决方案（10分钟）

**步骤1：** 打开 Visual Studio

**步骤2：** 点击 `文件` -> `新建` -> `项目`

**步骤3：** 选择 `Windows 窗体应用(.NET Framework)`

**步骤4：** 填写项目信息

* 项目名称：`PLCHttpTester`
* 位置：选择你的工作目录
* Framework：选择 `.NET Framework 4.5` 或更高
* 点击 `创建`
### 2.2 创建项目结构（20分钟）

**步骤1：** 在解决方案资源管理器中，右键点击项目名称

**步骤2：** 依次创建以下文件夹：

```
PLCHttpTester/
├── Core/
│   ├── Models/
│   ├── Parsers/
│   ├── Processors/
│   └── Services/
└── Utils/
```

创建方法：右键项目 -> `添加` -> `新建文件夹`

### 2.3 创建枚举文件（30分钟）

**步骤1：** 右键 `Models` 文件夹 -> `添加` -> `类`

**步骤2：** 命名为 `Enums.cs`

**步骤3：** 完整输入以下代码：

csharp

```
namespace PLCHttpTester.Core.Models
{
    /// <summary>
    /// 变量数据类型
    /// </summary>
    public enum VariableType
    {
        Bool,       // 布尔类型
        Int,        // 整数类型
        Float,      // 浮点数类型
        String,     // 字符串类型
        DateTime    // 日期时间类型
    }

    /// <summary>
    /// 变量来源
    /// </summary>
    public enum VariableSource
    {
        Request,    // 来自请求（需要用户输入）
        Response    // 来自响应（自动提取）
    }

    /// <summary>
    /// 表达式位置
    /// </summary>
    public enum ExpressionLocation
    {
        Url,        // 在URL中
        Header,     // 在Header中
        Body        // 在Body中
    }

    /// <summary>
    /// 逻辑运算符
    /// </summary>
    public enum LogicalOperator
    {
        And,        // &&
        Or          // ||
    }

    /// <summary>
    /// 比较运算符
    /// </summary>
    public enum ComparisonOperator
    {
        Equal,              // =
        GreaterThan,        // >
        LessThan,           // 
        GreaterOrEqual,     // >=
        LessOrEqual,        // <=
        Like                // like (字符串包含)
    }
}
```

**验证：** 按 `F6` 编译项目，确保没有错误

### 2.4 创建工具类（40分钟）

**步骤1：** 在 `Utils` 文件夹下创建 `StringHelper.cs`

csharp

```
using System;
using System.Text.RegularExpressions;

namespace PLCHttpTester.Utils
{
    /// <summary>
    /// 字符串辅助工具类
    /// </summary>
    public static class StringHelper
    {
        /// <summary>
        /// 生成唯一ID
        /// </summary>
        private static int _counter = 0;
        
        public static string GenerateUniqueId()
        {
            return $"VAR_{++_counter}";
        }

        /// <summary>
        /// 重置ID计数器
        /// </summary>
        public static void ResetIdCounter()
        {
            _counter = 0;
        }

        /// <summary>
        /// 转义JSON字符串中的特殊字符
        /// </summary>
        public static string EscapeJsonString(string str)
        {
            if (string.IsNullOrEmpty(str))
                return str;

            return str.Replace("\\", "\\\\")
                      .Replace("\"", "\\\"")
                      .Replace("\n", "\\n")
                      .Replace("\r", "\\r")
                      .Replace("\t", "\\t");
        }

        /// <summary>
        /// 判断字符串是否为占位符格式 ${ID}$
        /// </summary>
        public static bool IsPlaceholder(string str)
        {
            if (string.IsNullOrEmpty(str))
                return false;

            return Regex.IsMatch(str, @"^\$\{[A-Za-z0-9_]+\}\$$");
        }

        /// <summary>
        /// 从占位符中提取ID
        /// </summary>
        public static string ExtractIdFromPlaceholder(string placeholder)
        {
            // ${VAR_1}$ -> VAR_1
            Match match = Regex.Match(placeholder, @"^\$\{([A-Za-z0-9_]+)\}\$$");
            if (match.Success)
                return match.Groups[1].Value;
            return null;
        }

        /// <summary>
        /// 创建占位符
        /// </summary>
        public static string CreatePlaceholder(string id)
        {
            return $"${{{id}}}$";
        }
    }
}
```

**步骤2：** 按 `F6` 编译，确保无错误

### 2.5 第一天总结检查

**完成情况检查清单：**

* 项目已创建
* 文件夹结构已建立
* Enums.cs 已创建并编译通过
* StringHelper.cs 已创建并编译通过
* 整个解决方案编译无错误
**第一天产出：**

* 1个解决方案
* 5个文件夹
* 2个代码文件
---

## 3. 第二天：数据模型和变量管理

### 3.1 创建Variable类（30分钟）

**步骤1：** 在 `Models` 文件夹下创建 `Variable.cs`

csharp

```
using System;

namespace PLCHttpTester.Core.Models
{
    /// <summary>
    /// 变量类 - 表示一个可以在请求/响应中使用的变量
    /// </summary>
    public class Variable
    {
        /// <summary>
        /// 变量名（唯一标识）
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 数据类型
        /// </summary>
        public VariableType Type { get; set; }

        /// <summary>
        /// 变量的值
        /// </summary>
        public object Value { get; set; }

        /// <summary>
        /// 格式化字符串（如日期格式：yyyy-MM-dd）
        /// </summary>
        public string FormatString { get; set; }

        /// <summary>
        /// 变量来源
        /// </summary>
        public VariableSource Source { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public Variable()
        {
        }

        /// <summary>
        /// 带参数的构造函数
        /// </summary>
        public Variable(string name, VariableType type, VariableSource source, string formatString = null)
        {
            Name = name;
            Type = type;
            Source = source;
            FormatString = formatString;
            Value = GetDefaultValue(type);
        }

        /// <summary>
        /// 获取类型的默认值
        /// </summary>
        private object GetDefaultValue(VariableType type)
        {
            switch (type)
            {
                case VariableType.Bool:
                    return false;
                case VariableType.Int:
                    return 0;
                case VariableType.Float:
                    return 0.0;
                case VariableType.String:
                    return string.Empty;
                case VariableType.DateTime:
                    return DateTime.Now;
                default:
                    return null;
            }
        }

        /// <summary>
        /// 获取格式化后的字符串值
        /// </summary>
        public string GetFormattedValue()
        {
            if (Value == null)
                return string.Empty;

            // 如果是DateTime类型且有格式化字符串
            if (Type == VariableType.DateTime && !string.IsNullOrEmpty(FormatString))
            {
                if (Value is DateTime dt)
                    return dt.ToString(FormatString);
            }

            // 布尔类型转小写
            if (Type == VariableType.Bool)
            {
                return Value.ToString().ToLower();
            }

            return Value.ToString();
        }

        /// <summary>
        /// 设置变量值（带类型转换）
        /// </summary>
        public bool TrySetValue(string valueString)
        {
            try
            {
                switch (Type)
                {
                    case VariableType.Bool:
                        Value = bool.Parse(valueString);
                        return true;
                    case VariableType.Int:
                        Value = int.Parse(valueString);
                        return true;
                    case VariableType.Float:
                        Value = double.Parse(valueString);
                        return true;
                    case VariableType.String:
                        Value = valueString;
                        return true;
                    case VariableType.DateTime:
                        Value = DateTime.Parse(valueString);
                        return true;
                    default:
                        return false;
                }
            }
            catch
            {
                return false;
            }
        }

        public override string ToString()
        {
            return $"{Name} ({Type}) = {Value}";
        }
    }
}
```

**验证：** 编译项目，确保无错误

### 3.2 创建TemplateExpression类（30分钟）

**步骤1：** 在 `Models` 文件夹下创建 `TemplateExpression.cs`

csharp

```
namespace PLCHttpTester.Core.Models
{
    /// <summary>
    /// 模板表达式 - 表示模板中的一个变量引用
    /// 例如：@String(CurrentTime:yyyy-MM-dd HH:mm:ss.fff)
    /// </summary>
    public class TemplateExpression
    {
        /// <summary>
        /// 唯一标识符（用于替换）
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// 变量名
        /// </summary>
        public string VariableName { get; set; }

        /// <summary>
        /// 数据类型（Body中的表达式才有）
        /// </summary>
        public VariableType? DataType { get; set; }

        /// <summary>
        /// 格式化字符串
        /// </summary>
        public string FormatString { get; set; }

        /// <summary>
        /// 原始文本
        /// </summary>
        public string OriginalText { get; set; }

        /// <summary>
        /// 表达式位置
        /// </summary>
        public ExpressionLocation Location { get; set; }

        public override string ToString()
        {
            return $"[{Id}] {VariableName} ({DataType}) at {Location}";
        }
    }
}
```

### 3.3 创建HTTP模板类（40分钟）

**步骤1：** 在 `Models` 文件夹下创建 `HttpRequestTemplate.cs`

csharp

```
using System.Collections.Generic;

namespace PLCHttpTester.Core.Models
{
    /// <summary>
    /// HTTP请求模板
    /// </summary>
    public class HttpRequestTemplate
    {
        /// <summary>
        /// HTTP方法（GET/POST/PUT/DELETE）
        /// </summary>
        public string Method { get; set; }

        /// <summary>
        /// URL路径（可能包含占位符）
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// 请求头
        /// </summary>
        public Dictionary<string, string> Headers { get; set; }

        /// <summary>
        /// Body模板字符串（已替换为占位符）
        /// </summary>
        public string BodyTemplate { get; set; }

        /// <summary>
        /// 所有提取的表达式
        /// </summary>
        public List<TemplateExpression> Expressions { get; set; }

        /// <summary>
        /// 原始的完整模板文本
        /// </summary>
        public string OriginalText { get; set; }

        public HttpRequestTemplate()
        {
            Headers = new Dictionary<string, string>();
            Expressions = new List<TemplateExpression>();
        }
    }
}
```

**步骤2：** 在 `Models` 文件夹下创建 `HttpResponseTemplate.cs`

csharp

```
using System.Collections.Generic;

namespace PLCHttpTester.Core.Models
{
    /// <summary>
    /// HTTP响应模板
    /// </summary>
    public class HttpResponseTemplate
    {
        /// <summary>
        /// 期望的状态码
        /// </summary>
        public int? ExpectedStatusCode { get; set; }

        /// <summary>
        /// 响应头
        /// </summary>
        public Dictionary<string, string> Headers { get; set; }

        /// <summary>
        /// Body模板字符串（已替换为占位符）
        /// </summary>
        public string BodyTemplate { get; set; }

        /// <summary>
        /// 响应映射列表（JSON路径到变量）
        /// </summary>
        public List<ResponseMapping> Mappings { get; set; }

        /// <summary>
        /// 原始的完整模板文本
        /// </summary>
        public string OriginalText { get; set; }

        public HttpResponseTemplate()
        {
            Headers = new Dictionary<string, string>();
            Mappings = new List<ResponseMapping>();
        }
    }
}
```

**步骤3：** 在 `Models` 文件夹下创建 `ResponseMapping.cs`

csharp

```
namespace PLCHttpTester.Core.Models
{
    /// <summary>
    /// 响应映射 - 描述如何从JSON响应中提取变量
    /// </summary>
    public class ResponseMapping
    {
        /// <summary>
        /// 表达式ID
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// JSON Pointer路径（如 /ticket/token）
        /// </summary>
        public string JsonPointer { get; set; }

        /// <summary>
        /// 变量名
        /// </summary>
        public string VariableName { get; set; }

        /// <summary>
        /// 数据类型
        /// </summary>
        public VariableType DataType { get; set; }

        public override string ToString()
        {
            return $"{VariableName} <- {JsonPointer}";
        }
    }
}
```

### 3.4 创建变量管理器（50分钟）

**步骤1：** 在 `Services` 文件夹下创建 `VariableManager.cs`

csharp

```
using System;
using System.Collections.Generic;
using System.Linq;
using PLCHttpTester.Core.Models;

namespace PLCHttpTester.Core.Services
{
    /// <summary>
    /// 变量管理器 - 负责管理所有变量
    /// </summary>
    public class VariableManager
    {
        // 存储所有变量，键为变量名
        private Dictionary<string, Variable> _variables;

        public VariableManager()
        {
            _variables = new Dictionary<string, Variable>();
        }

        /// <summary>
        /// 注册一个新变量
        /// </summary>
        public void RegisterVariable(Variable variable)
        {
            if (variable == null)
                throw new ArgumentNullException(nameof(variable));

            if (string.IsNullOrWhiteSpace(variable.Name))
                throw new ArgumentException("变量名不能为空");

            // 如果已存在同名变量，更新它
            if (_variables.ContainsKey(variable.Name))
            {
                _variables[variable.Name] = variable;
            }
            else
            {
                _variables.Add(variable.Name, variable);
            }
        }

        /// <summary>
        /// 获取变量
        /// </summary>
        public Variable GetVariable(string name)
        {
            if (_variables.ContainsKey(name))
                return _variables[name];
            return null;
        }

        /// <summary>
        /// 设置变量值
        /// </summary>
        public bool SetVariableValue(string name, object value)
        {
            Variable variable = GetVariable(name);
            if (variable == null)
                return false;

            variable.Value = value;
            return true;
        }

        /// <summary>
        /// 获取所有变量
        /// </summary>
        public Dictionary<string, Variable> GetAllVariables()
        {
            return new Dictionary<string, Variable>(_variables);
        }

        /// <summary>
        /// 获取请求变量列表（需要用户输入的）
        /// </summary>
        public List<Variable> GetRequestVariables()
        {
            return _variables.Values
                .Where(v => v.Source == VariableSource.Request)
                .OrderBy(v => v.Name)
                .ToList();
        }

        /// <summary>
        /// 获取响应变量列表（从响应中提取的）
        /// </summary>
        public List<Variable> GetResponseVariables()
        {
            return _variables.Values
                .Where(v => v.Source == VariableSource.Response)
                .OrderBy(v => v.Name)
                .ToList();
        }

        /// <summary>
        /// 检查是否所有请求变量都已赋值
        /// </summary>
        public bool AreAllRequestVariablesSet()
        {
            var requestVars = GetRequestVariables();
            foreach (var variable in requestVars)
            {
                // 检查是否为默认值或空值
                if (variable.Value == null)
                    return false;

                if (variable.Type == VariableType.String && string.IsNullOrEmpty(variable.Value.ToString()))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 获取未赋值的变量列表
        /// </summary>
        public List<string> GetUnsetVariableNames()
        {
            var unsetVars = new List<string>();
            var requestVars = GetRequestVariables();

            foreach (var variable in requestVars)
            {
                if (variable.Value == null || 
                    (variable.Type == VariableType.String && string.IsNullOrEmpty(variable.Value.ToString())))
                {
                    unsetVars.Add(variable.Name);
                }
            }

            return unsetVars;
        }

        /// <summary>
        /// 清除所有变量
        /// </summary>
        public void Clear()
        {
            _variables.Clear();
        }

        /// <summary>
        /// 变量数量
        /// </summary>
        public int Count => _variables.Count;
    }
}
```

**验证：** 编译项目，确保无错误

### 3.5 编写测试代码（30分钟）

**步骤1：** 在 `Form1.cs` 中添加测试代码

打开 `Form1.cs`，在类中添加一个按钮点击事件来测试：

csharp

```
using System;
using System.Windows.Forms;
using PLCHttpTester.Core.Models;
using PLCHttpTester.Core.Services;

namespace PLCHttpTester
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            TestVariableManager();
        }

        // 测试变量管理器
        private void TestVariableManager()
        {
            try
            {
                var manager = new VariableManager();

                // 创建一些测试变量
                var var1 = new Variable("StationID", VariableType.Int, VariableSource.Request);
                var1.Value = 123;

                var var2 = new Variable("CurrentTime", VariableType.DateTime, VariableSource.Request, "yyyy-MM-dd HH:mm:ss");
                var2.Value = DateTime.Now;

                var var3 = new Variable("Start_Success", VariableType.Bool, VariableSource.Response);

                // 注册变量
                manager.RegisterVariable(var1);
                manager.RegisterVariable(var2);
                manager.RegisterVariable(var3);

                // 测试获取变量
                var requestVars = manager.GetRequestVariables();
                var responseVars = manager.GetResponseVariables();

                MessageBox.Show($"测试成功！\n" +
                    $"请求变量数量: {requestVars.Count}\n" +
                    $"响应变量数量: {responseVars.Count}\n" +
                    $"总变量数量: {manager.Count}",
                    "测试结果");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"测试失败: {ex.Message}", "错误");
            }
        }
    }
}
```

**步骤2：** 按 `F5` 运行程序，应该看到测试成功的消息框

### 3.6 第二天总结检查

**完成情况检查清单：**

* Variable.cs 已创建并测试
* TemplateExpression.cs 已创建
* HttpRequestTemplate.cs 已创建
* HttpResponseTemplate.cs 已创建
* ResponseMapping.cs 已创建
* VariableManager.cs 已创建并测试
* 测试代码运行成功
**第二天产出：**

* 6个模型类
* 1个服务类
* 基础功能已验证
---

## 4. 第三天：请求模板解析器

### 4.1 创建正则表达式工具类（30分钟）

**步骤1：** 在 `Utils` 文件夹下创建 `RegexPatterns.cs`

csharp

```
namespace PLCHttpTester.Utils
{
    /// <summary>
    /// 正则表达式模式集合
    /// </summary>
    public static class RegexPatterns
    {
        /// <summary>
        /// URL中的变量模式：@(VarName) 或 @(VarName:format)
        /// 示例：@(StationID:d), @(Test)
        /// </summary>
        public const string UrlVariable = @"@\((\w+)(?::([^)]+))?\)";

        /// <summary>
        /// Header中的变量模式：@(VarName) 或 @(VarName:format)
        /// </summary>
        public const string HeaderVariable = @"@\((\w+)(?::([^)]+))?\)";

        /// <summary>
        /// Body中的变量模式：@Type(VarName) 或 @Type(VarName:format)
        /// 示例：@String(CurrentTime:yyyy-MM-dd), @Number(Count)
        /// </summary>
        public const string BodyVariable = @"@(Bool|Int|Float|String|DateTime|Number)\((\w+)(?::([^)]+))?\)";

        /// <summary>
        /// HTTP请求行模式：METHOD /path
        /// 示例：POST /api/mes/start
        /// </summary>
        public const string RequestLine = @"^(GET|POST|PUT|DELETE|PATCH)\s+(.+)$";

        /// <summary>
        /// HTTP响应状态行模式：CODE Message
        /// 示例：200 OK
        /// </summary>
        public const string StatusLine = @"^(\d{3})\s+(.*)$";

        /// <summary>
        /// Header行模式：Key: Value
        /// 示例：Content-Type: application/json
        /// </summary>
        public const string HeaderLine = @"^([^:]+):\s*(.*)$";
    }
}
```

### 4.2 创建RequestTemplateParser - 第一部分（60分钟）

**步骤1：** 在 `Parsers` 文件夹下创建 `RequestTemplateParser.cs`

csharp

```
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using PLCHttpTester.Core.Models;
using PLCHttpTester.Utils;

namespace PLCHttpTester.Core.Parsers
{
    /// <summary>
    /// 请求模板解析器
    /// </summary>
    public class RequestTemplateParser
    {
        /// <summary>
        /// 解析请求模板
        /// </summary>
        public HttpRequestTemplate Parse(string templateText)
        {
            if (string.IsNullOrWhiteSpace(templateText))
                throw new ArgumentException("模板文本不能为空");

            // 重置ID计数器
            StringHelper.ResetIdCounter();

            var template = new HttpRequestTemplate
            {
                OriginalText = templateText
            };

            // 步骤1：按空行分割Header和Body
            string[] parts = SplitHeaderAndBody(templateText);
            string headerSection = parts[0];
            string bodySection = parts.Length > 1 ? parts[1] : string.Empty;

            // 步骤2：解析Header部分
            ParseHeaderSection(headerSection, template);

            // 步骤3：解析Body部分
            if (!string.IsNullOrWhiteSpace(bodySection))
            {
                ParseBodySection(bodySection, template);
            }

            return template;
        }

        /// <summary>
        /// 分割Header和Body
        /// </summary>
        private string[] SplitHeaderAndBody(string text)
        {
            // 查找第一个空行
            string[] lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            
            int emptyLineIndex = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                {
                    emptyLineIndex = i;
                    break;
                }
            }

            if (emptyLineIndex == -1)
            {
                // 没有Body
                return new[] { text };
            }

            // 分割
            StringBuilder header = new StringBuilder();
            StringBuilder body = new StringBuilder();

            for (int i = 0; i < emptyLineIndex; i++)
            {
                header.AppendLine(lines[i]);
            }

            for (int i = emptyLineIndex + 1; i < lines.Length; i++)
            {
                body.AppendLine(lines[i]);
            }

            return new[] { header.ToString().Trim(), body.ToString().Trim() };
        }

        /// <summary>
        /// 解析Header部分
        /// </summary>
        private void ParseHeaderSection(string headerSection, HttpRequestTemplate template)
        {
            string[] lines = headerSection.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length == 0)
                throw new Exception("请求模板格式错误：缺少请求行");

            // 解析第一行（请求行）
            ParseRequestLine(lines[0], template);

            // 解析其他Header行
            for (int i = 1; i < lines.Length; i++)
            {
                ParseHeaderLine(lines[i], template);
            }
        }

        /// <summary>
        /// 解析请求行（第一行）
        /// 示例：POST /api/mes/start?stationId=@(StationID:d)&whatever=@(Test)
        /// </summary>
        private void ParseRequestLine(string line, HttpRequestTemplate template)
        {
            Match match = Regex.Match(line, RegexPatterns.RequestLine);
            if (!match.Success)
                throw new Exception($"请求行格式错误: {line}");

            template.Method = match.Groups[1].Value;
            string urlPart = match.Groups[2].Value;

            // 提取URL中的变量并替换
            template.Url = ProcessUrlVariables(urlPart, template);
        }

        /// <summary>
        /// 处理URL中的变量
        /// </summary>
        private string ProcessUrlVariables(string url, HttpRequestTemplate template)
        {
            return Regex.Replace(url, RegexPatterns.UrlVariable, match =>
            {
                string varName = match.Groups[1].Value;
                string format = match.Groups[2].Success ? match.Groups[2].Value : null;

                // 创建表达式对象
                var expression = new TemplateExpression
                {
                    Id = StringHelper.GenerateUniqueId(),
                    VariableName = varName,
                    DataType = null,  // URL中不指定类型
                    FormatString = format,
                    OriginalText = match.Value,
                    Location = ExpressionLocation.Url
                };

                template.Expressions.Add(expression);

                // 不替换为占位符，保持原样（URL中直接替换值）
                return match.Value;
            });
        }

        /// <summary>
        /// 解析Header行
        /// 示例：User-Agent: BYD_PLC2MES
        ///      Cookies: UserAuth=@(AuthCookie)
        /// </summary>
        private void ParseHeaderLine(string line, HttpRequestTemplate template)
        {
            Match match = Regex.Match(line, RegexPatterns.HeaderLine);
            if (!match.Success)
                return;  // 跳过格式不正确的行

            string key = match.Groups[1].Value.Trim();
            string value = match.Groups[2].Value.Trim();

            // 处理值中的变量
            value = ProcessHeaderVariables(value, template);

            template.Headers[key] = value;
        }

        /// <summary>
        /// 处理Header中的变量
        /// </summary>
        private string ProcessHeaderVariables(string headerValue, HttpRequestTemplate template)
        {
            return Regex.Replace(headerValue, RegexPatterns.HeaderVariable, match =>
            {
                string varName = match.Groups[1].Value;
                string format = match.Groups[2].Success ? match.Groups[2].Value : null;

                var expression = new TemplateExpression
                {
                    Id = StringHelper.GenerateUniqueId(),
                    VariableName = varName,
                    DataType = null,
                    FormatString = format,
                    OriginalText = match.Value,
                    Location = ExpressionLocation.Header
                };

                template.Expressions.Add(expression);

                // 不替换为占位符
                return match.Value;
            });
        }

        // 下一步继续...
    }
}
```

### 4.3 创建RequestTemplateParser - 第二部分（60分钟）

继续在 `RequestTemplateParser.cs` 中添加Body解析方法：

csharp

```
/// <summary>
        /// 解析Body部分
        /// </summary>
        private void ParseBodySection(string bodySection, HttpRequestTemplate template)
        {
            // 将Body中的表达式替换为占位符
            string processedBody = ProcessBodyVariables(bodySection, template);
            template.BodyTemplate = processedBody;
        }

        /// <summary>
        /// 处理Body中的变量
        /// Body中的变量格式：@Type(VarName) 或 @Type(VarName:format)
        /// </summary>
        private string ProcessBodyVariables(string body, HttpRequestTemplate template)
        {
            return Regex.Replace(body, RegexPatterns.BodyVariable, match =>
            {
                string typeStr = match.Groups[1].Value;
                string varName = match.Groups[2].Value;
                string format = match.Groups[3].Success ? match.Groups[3].Value : null;

                // 将类型字符串转换为枚举
                VariableType varType = ParseVariableType(typeStr);

                var expression = new TemplateExpression
                {
                    Id = StringHelper.GenerateUniqueId(),
                    VariableName = varName,
                    DataType = varType,
                    FormatString = format,
                    OriginalText = match.Value,
                    Location = ExpressionLocation.Body
                };

                template.Expressions.Add(expression);

                // 替换为占位符 "${ID}$"
                return StringHelper.CreatePlaceholder(expression.Id);
            });
        }

        /// <summary>
        /// 将类型字符串转换为VariableType枚举
        /// </summary>
        private VariableType ParseVariableType(string typeStr)
        {
            switch (typeStr.ToLower())
            {
                case "bool":
                    return VariableType.Bool;
                case "int":
                case "number":
                    return VariableType.Int;
                case "float":
                    return VariableType.Float;
                case "string":
                    return VariableType.String;
                case "datetime":
                    return VariableType.DateTime;
                default:
                    throw new Exception($"不支持的数据类型: {typeStr}");
            }
        }
```

### 4.4 测试请求模板解析器（40分钟）

**步骤1：** 在 `Form1.cs` 中添加测试方法

csharp

```
private void TestRequestParser()
{
    try
    {
        string template = @"POST /api/mes/start?stationId=@(StationID:d)&whatever=@(Test)
User-Agent: BYD_PLC2MES
Content-Type: application/json
Cookies: UserAuth=@(AuthCookie)

{
""start_time"": @String(CurrentTime:yyyy-MM-dd HH:mm:ss.fff),
""sn"": @String(SerialNo),
""count"": @Number(Count),
""done"": @Bool(Done)
}";

        var parser = new RequestTemplateParser();
        var result = parser.Parse(template);

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"Method: {result.Method}");
        sb.AppendLine($"URL: {result.Url}");
        sb.AppendLine($"Headers: {result.Headers.Count}");
        sb.AppendLine($"Expressions: {result.Expressions.Count}");
        sb.AppendLine();
        sb.AppendLine("Body Template:");
        sb.AppendLine(result.BodyTemplate);
        sb.AppendLine();
        sb.AppendLine("Expressions:");
        foreach (var exp in result.Expressions)
        {
            sb.AppendLine($"  - {exp.VariableName} ({exp.DataType}) at {exp.Location}");
        }

        MessageBox.Show(sb.ToString(), "解析结果");
    }
    catch (Exception ex)
    {
        MessageBox.Show($"解析失败: {ex.Message}\n{ex.StackTrace}", "错误");
    }
}
```

**步骤2：** 修改构造函数调用测试

csharp

```
public Form1()
{
    InitializeComponent();
    TestRequestParser();
}
```

**步骤3：** 按 `F5` 运行，检查解析结果是否正确

**预期结果：**

* Method: POST
* URL包含变量
* Headers应该有4个
* Expressions应该有7个（URL 2个，Header 1个，Body 4个）
* BodyTemplate中应该看到 `${VAR_1}$` 这样的占位符
### 4.5 第三天总结检查

**完成情况检查清单：**

* RegexPatterns.cs 已创建
* RequestTemplateParser.cs 已创建
* 能正确解析请求行
* 能正确解析Headers
* 能正确提取URL中的变量
* 能正确提取Body中的变量
* Body中的变量已替换为占位符
* 测试通过
**第三天产出：**

* 1个工具类（正则表达式）
* 1个解析器类（请求模板）
* 完整的请求解析功能
---

## 5. 第四天：响应模板解析器

### 5.1 创建JSON处理基础类（40分钟）

**步骤1：** 在 `Processors` 文件夹下创建 `JsonProcessor.cs`

csharp

```
using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace PLCHttpTester.Core.Processors
{
    /// <summary>
    /// JSON处理器 - 使用内置的JavaScriptSerializer
    /// </summary>
    public class JsonProcessor
    {
        private JavaScriptSerializer _serializer;

        public JsonProcessor()
        {
            _serializer = new JavaScriptSerializer();
            _serializer.MaxJsonLength = int.MaxValue;
        }

        /// <summary>
        /// 将JSON字符串解析为对象
        /// </summary>
        public object ParseJson(string json)
        {
            try
            {
                return _serializer.DeserializeObject(json);
            }
            catch (Exception ex)
            {
                throw new Exception($"JSON解析失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 将对象转换为JSON字符串
        /// </summary>
        public string ToJson(object obj)
        {
            try
            {
                return _serializer.Serialize(obj);
            }
            catch (Exception ex)
            {
                throw new Exception($"JSON序列化失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 根据JSON Pointer获取值
        /// JSON Pointer格式：/path/to/value
        /// </summary>
        public object GetValueByPointer(object root, string jsonPointer)
        {
            if (root == null)
                return null;

            // 空指针或根指针
            if (string.IsNullOrEmpty(jsonPointer) || jsonPointer == "/")
                return root;

            // 分割路径
            string[] segments = jsonPointer.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            
            object current = root;

            foreach (string segment in segments)
            {
                // 处理转义字符 ~1 -> / , ~0 -> ~
                string unescaped = segment.Replace("~1", "/").Replace("~0", "~");

                if (current is Dictionary<string, object> dict)
                {
                    if (dict.ContainsKey(unescaped))
                    {
                        current = dict[unescaped];
                    }
                    else
                    {
                        return null;  // 路径不存在
                    }
                }
                else if (current is object[] array)
                {
                    if (int.TryParse(unescaped, out int index) && index >= 0 && index < array.Length)
                    {
                        current = array[index];
                    }
                    else
                    {
                        return null;
                    }
                }
                else
                {
                    return null;  // 无法继续遍历
                }
            }

            return current;
        }

        /// <summary>
        /// 遍历JSON对象树，找到所有符合条件的路径
        /// </summary>
        public void TraverseJson(object obj, string currentPath, Action<string, object> callback)
        {
            if (obj == null)
                return;

            if (obj is Dictionary<string, object> dict)
            {
                foreach (var kvp in dict)
                {
                    string newPath = currentPath + "/" + EscapeJsonPointer(kvp.Key);
                    callback(newPath, kvp.Value);
                    TraverseJson(kvp.Value, newPath, callback);
                }
            }
            else if (obj is object[] array)
            {
                for (int i = 0; i < array.Length; i++)
                {
                    string newPath = currentPath + "/" + i;
                    callback(newPath, array[i]);
                    TraverseJson(array[i], newPath, callback);
                }
            }
        }

        /// <summary>
        /// 转义JSON Pointer中的特殊字符
        /// </summary>
        private string EscapeJsonPointer(string segment)
        {
            return segment.Replace("~", "~0").Replace("/", "~1");
        }
    }
}
```

**注意：** 需要添加引用 `System.Web.Extensions`

* 右键项目 -> 添加 -> 引用 -> 程序集 -> 搜索 `System.Web.Extensions` -> 勾选 -> 确定
### 5.2 创建ResponseTemplateParser（90分钟）

**步骤1：** 在 `Parsers` 文件夹下创建 `ResponseTemplateParser.cs`

csharp

```
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using PLCHttpTester.Core.Models;
using PLCHttpTester.Core.Processors;
using PLCHttpTester.Utils;

namespace PLCHttpTester.Core.Parsers
{
    /// <summary>
    /// 响应模板解析器
    /// </summary>
    public class ResponseTemplateParser
    {
        private JsonProcessor _jsonProcessor;

        public ResponseTemplateParser()
        {
            _jsonProcessor = new JsonProcessor();
        }

        /// <summary>
        /// 解析响应模板
        /// </summary>
        public HttpResponseTemplate Parse(string templateText)
        {
            if (string.IsNullOrWhiteSpace(templateText))
                throw new ArgumentException("响应模板文本不能为空");

            var template = new HttpResponseTemplate
            {
                OriginalText = templateText
            };

            // 步骤1：分割Header和Body
            string[] parts = SplitHeaderAndBody(templateText);
            string headerSection = parts[0];
            string bodySection = parts.Length > 1 ? parts[1] : string.Empty;

            // 步骤2：解析Header部分（状态行和响应头）
            ParseHeaderSection(headerSection, template);

            // 步骤3：解析Body部分
            if (!string.IsNullOrWhiteSpace(bodySection))
            {
                ParseBodySection(bodySection, template);
            }

            return template;
        }

        /// <summary>
        /// 分割Header和Body
        /// </summary>
        private string[] SplitHeaderAndBody(string text)
        {
            string[] lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            
            int emptyLineIndex = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                {
                    emptyLineIndex = i;
                    break;
                }
            }

            if (emptyLineIndex == -1)
                return new[] { text };

            StringBuilder header = new StringBuilder();
            StringBuilder body = new StringBuilder();

            for (int i = 0; i < emptyLineIndex; i++)
            {
                header.AppendLine(lines[i]);
            }

            for (int i = emptyLineIndex + 1; i < lines.Length; i++)
            {
                body.AppendLine(lines[i]);
            }

            return new[] { header.ToString().Trim(), body.ToString().Trim() };
        }

        /// <summary>
        /// 解析Header部分
        /// </summary>
        private void ParseHeaderSection(string headerSection, HttpResponseTemplate template)
        {
            string[] lines = headerSection.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length == 0)
                throw new Exception("响应模板格式错误：缺少状态行");

            // 解析第一行（状态行）
            ParseStatusLine(lines[0], template);

            // 解析其他Header行
            for (int i = 1; i < lines.Length; i++)
            {
                ParseHeaderLine(lines[i], template);
            }
        }

        /// <summary>
        /// 解析状态行
        /// 示例：200 OK
        /// </summary>
        private void ParseStatusLine(string line, HttpResponseTemplate template)
        {
            Match match = Regex.Match(line, RegexPatterns.StatusLine);
            if (match.Success)
            {
                template.ExpectedStatusCode = int.Parse(match.Groups[1].Value);
            }
        }

        /// <summary>
        /// 解析Header行
        /// </summary>
        private void ParseHeaderLine(string line, HttpResponseTemplate template)
        {
            Match match = Regex.Match(line, RegexPatterns.HeaderLine);
            if (match.Success)
            {
                string key = match.Groups[1].Value.Trim();
                string value = match.Groups[2].Value.Trim();
                template.Headers[key] = value;
            }
        }

        /// <summary>
        /// 解析Body部分
        /// </summary>
        private void ParseBodySection(string bodySection, HttpResponseTemplate template)
        {
            // 步骤1：提取所有表达式并替换为占位符
            var expressions = new List<TemplateExpression>();
            string processedBody = ProcessBodyVariables(bodySection, expressions);
            template.BodyTemplate = processedBody;

            // 步骤2：将处理后的Body解析为JSON对象
            try
            {
                object jsonObj = _jsonProcessor.ParseJson(processedBody);

                // 步骤3：遍历JSON树，记录每个占位符的路径
                _jsonProcessor.TraverseJson(jsonObj, "", (path, value) =>
                {
                    if (value is string strValue && StringHelper.IsPlaceholder(strValue))
                    {
```

EX

Continue

markdown

```
// 从占位符提取ID
                        string id = StringHelper.ExtractIdFromPlaceholder(strValue);
                        if (id != null)
                        {
                            // 找到对应的表达式
                            var expression = expressions.Find(e => e.Id == id);
                            if (expression != null)
                            {
                                // 创建响应映射
                                var mapping = new ResponseMapping
                                {
                                    Id = id,
                                    JsonPointer = path,
                                    VariableName = expression.VariableName,
                                    DataType = expression.DataType.Value
                                };
                                template.Mappings.Add(mapping);
                            }
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                throw new Exception($"响应Body JSON解析失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理Body中的变量，提取并替换为占位符
        /// </summary>
        private string ProcessBodyVariables(string body, List<TemplateExpression> expressions)
        {
            return Regex.Replace(body, RegexPatterns.BodyVariable, match =>
            {
                string typeStr = match.Groups[1].Value;
                string varName = match.Groups[2].Value;
                string format = match.Groups[3].Success ? match.Groups[3].Value : null;

                VariableType varType = ParseVariableType(typeStr);

                var expression = new TemplateExpression
                {
                    Id = StringHelper.GenerateUniqueId(),
                    VariableName = varName,
                    DataType = varType,
                    FormatString = format,
                    OriginalText = match.Value,
                    Location = ExpressionLocation.Body
                };

                expressions.Add(expression);

                // 替换为占位符
                return StringHelper.CreatePlaceholder(expression.Id);
            });
        }

        /// <summary>
        /// 将类型字符串转换为枚举
        /// </summary>
        private VariableType ParseVariableType(string typeStr)
        {
            switch (typeStr.ToLower())
            {
                case "bool":
                    return VariableType.Bool;
                case "int":
                case "number":
                    return VariableType.Int;
                case "float":
                    return VariableType.Float;
                case "string":
                    return VariableType.String;
                case "datetime":
                    return VariableType.DateTime;
                default:
                    throw new Exception($"不支持的数据类型: {typeStr}");
            }
        }
    }
}
```

### 5.3 测试响应模板解析器（40分钟）

**步骤1：** 在 `Form1.cs` 中添加测试方法

csharp

```
private void TestResponseParser()
{
    try
    {
        string template = @"200 OK 
Content-Type: application/json

{
    ""success"": @Bool(Start_Success),
    ""message"": @String(Start_Message),
    ""ticket"": {
        ""token"": @Number(Start_Token),
        ""expires"": @String(Start_TokenExpires)
    }
}";

        var parser = new ResponseTemplateParser();
        var result = parser.Parse(template);

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"Expected Status Code: {result.ExpectedStatusCode}");
        sb.AppendLine($"Headers: {result.Headers.Count}");
        sb.AppendLine($"Mappings: {result.Mappings.Count}");
        sb.AppendLine();
        sb.AppendLine("Body Template:");
        sb.AppendLine(result.BodyTemplate);
        sb.AppendLine();
        sb.AppendLine("Mappings:");
        foreach (var mapping in result.Mappings)
        {
            sb.AppendLine($"  {mapping.JsonPointer} -> {mapping.VariableName} ({mapping.DataType})");
        }

        MessageBox.Show(sb.ToString(), "响应解析结果");
    }
    catch (Exception ex)
    {
        MessageBox.Show($"解析失败: {ex.Message}\n{ex.StackTrace}", "错误");
    }
}
```

**步骤2：** 修改构造函数

csharp

```
public Form1()
{
    InitializeComponent();
    TestResponseParser();
}
```

**步骤3：** 按 `F5` 运行测试

**预期结果：**

* Expected Status Code: 200
* Mappings: 4
* JSON Pointer应该正确指向各个字段：
	* `/success` -> Start_Success
	* `/message` -> Start_Message
	* `/ticket/token` -> Start_Token
	* `/ticket/expires` -> Start_TokenExpires
### 5.4 第四天总结检查

**完成情况检查清单：**

* JsonProcessor.cs 已创建
* ResponseTemplateParser.cs 已创建
* 能正确解析状态行
* 能正确提取Body中的变量
* 能正确生成JSON Pointer路径
* 能正确创建ResponseMapping
* 测试通过
**第四天产出：**

* 1个处理器类（JSON处理）
* 1个解析器类（响应模板）
* 完整的响应解析功能
---

## 6. 第五天：成功条件解析器

### 6.1 创建条件节点类（45分钟）

**步骤1：** 在 `Models` 文件夹下创建 `ConditionNodes.cs`

csharp

```
using System;
using System.Collections.Generic;

namespace PLCHttpTester.Core.Models
{
    /// <summary>
    /// 条件节点基类
    /// </summary>
    public abstract class ConditionNode
    {
        public abstract bool Evaluate(Dictionary<string, Variable> variables);
    }

    /// <summary>
    /// 逻辑运算符节点（AND / OR）
    /// </summary>
    public class LogicalOperatorNode : ConditionNode
    {
        public LogicalOperator Operator { get; set; }
        public ConditionNode Left { get; set; }
        public ConditionNode Right { get; set; }

        public override bool Evaluate(Dictionary<string, Variable> variables)
        {
            bool leftResult = Left.Evaluate(variables);
            
            // 短路求值
            if (Operator == LogicalOperator.And && !leftResult)
                return false;
            if (Operator == LogicalOperator.Or && leftResult)
                return true;

            bool rightResult = Right.Evaluate(variables);
            return Operator == LogicalOperator.And ? leftResult && rightResult : leftResult || rightResult;
        }

        public override string ToString()
        {
            return $"({Left} {Operator} {Right})";
        }
    }

    /// <summary>
    /// 比较节点
    /// </summary>
    public class ComparisonNode : ConditionNode
    {
        public string VariableName { get; set; }
        public ComparisonOperator Operator { get; set; }
        public object CompareValue { get; set; }

        public override bool Evaluate(Dictionary<string, Variable> variables)
        {
            // 特殊变量：$StatusCode
            if (VariableName == "$StatusCode")
            {
                // 需要从特殊位置获取状态码
                // 这里暂时返回true，后续在实际使用时处理
                return true;
            }

            // 获取变量
            if (!variables.ContainsKey(VariableName))
            {
                // 变量不存在，使用默认值false
                return false;
            }

            Variable variable = variables[VariableName];
            object value = variable.Value;

            if (value == null)
                return false;

            try
            {
                switch (Operator)
                {
                    case ComparisonOperator.Equal:
                        return CompareEqual(value, CompareValue, variable.Type);

                    case ComparisonOperator.GreaterThan:
                        return CompareGreaterThan(value, CompareValue, variable.Type);

                    case ComparisonOperator.LessThan:
                        return CompareLessThan(value, CompareValue, variable.Type);

                    case ComparisonOperator.GreaterOrEqual:
                        return CompareGreaterThan(value, CompareValue, variable.Type) || 
                               CompareEqual(value, CompareValue, variable.Type);

                    case ComparisonOperator.LessOrEqual:
                        return CompareLessThan(value, CompareValue, variable.Type) || 
                               CompareEqual(value, CompareValue, variable.Type);

                    case ComparisonOperator.Like:
                        return CompareLike(value, CompareValue);

                    default:
                        return false;
                }
            }
            catch
            {
                return false;
            }
        }

        private bool CompareEqual(object value, object compareValue, VariableType type)
        {
            switch (type)
            {
                case VariableType.Bool:
                    return Convert.ToBoolean(value) == Convert.ToBoolean(compareValue);
                case VariableType.Int:
                    return Convert.ToInt32(value) == Convert.ToInt32(compareValue);
                case VariableType.Float:
                    return Math.Abs(Convert.ToDouble(value) - Convert.ToDouble(compareValue)) < 0.000001;
                case VariableType.String:
                    return value.ToString() == compareValue.ToString();
                case VariableType.DateTime:
                    return Convert.ToDateTime(value) == Convert.ToDateTime(compareValue);
                default:
                    return false;
            }
        }

        private bool CompareGreaterThan(object value, object compareValue, VariableType type)
        {
            switch (type)
            {
                case VariableType.Int:
                    return Convert.ToInt32(value) > Convert.ToInt32(compareValue);
                case VariableType.Float:
                    return Convert.ToDouble(value) > Convert.ToDouble(compareValue);
                case VariableType.DateTime:
                    return Convert.ToDateTime(value) > Convert.ToDateTime(compareValue);
                case VariableType.String:
                    return string.Compare(value.ToString(), compareValue.ToString()) > 0;
                default:
                    return false;
            }
        }

        private bool CompareLessThan(object value, object compareValue, VariableType type)
        {
            switch (type)
            {
                case VariableType.Int:
                    return Convert.ToInt32(value) < Convert.ToInt32(compareValue);
                case VariableType.Float:
                    return Convert.ToDouble(value) < Convert.ToDouble(compareValue);
                case VariableType.DateTime:
                    return Convert.ToDateTime(value) < Convert.ToDateTime(compareValue);
                case VariableType.String:
                    return string.Compare(value.ToString(), compareValue.ToString()) < 0;
                default:
                    return false;
            }
        }

        private bool CompareLike(object value, object compareValue)
        {
            string str = value.ToString();
            string pattern = compareValue.ToString();

            // 处理通配符 %
            // "token22%" 表示以 "token22" 开头
            if (pattern.EndsWith("%") && pattern.StartsWith("%"))
            {
                // %xxx% 包含
                string search = pattern.Substring(1, pattern.Length - 2);
                return str.Contains(search);
            }
            else if (pattern.EndsWith("%"))
            {
                // xxx% 开头
                string prefix = pattern.Substring(0, pattern.Length - 1);
                return str.StartsWith(prefix);
            }
            else if (pattern.StartsWith("%"))
            {
                // %xxx 结尾
                string suffix = pattern.Substring(1);
                return str.EndsWith(suffix);
            }
            else
            {
                // 精确匹配
                return str == pattern;
            }
        }

        public override string ToString()
        {
            return $"{VariableName} {Operator} {CompareValue}";
        }
    }

    /// <summary>
    /// 布尔值节点（变量名直接作为条件）
    /// </summary>
    public class BooleanVariableNode : ConditionNode
    {
        public string VariableName { get; set; }

        public override bool Evaluate(Dictionary<string, Variable> variables)
        {
            if (!variables.ContainsKey(VariableName))
                return false;

            Variable variable = variables[VariableName];
            
            // 如果是布尔类型，直接返回值
            if (variable.Type == VariableType.Bool)
            {
                return Convert.ToBoolean(variable.Value);
            }

            // 其他类型不支持直接作为布尔值
            return false;
        }

        public override string ToString()
        {
            return VariableName;
        }
    }
}
```

### 6.2 创建Token类（20分钟）

**步骤1：** 在 `Models` 文件夹下创建 `Token.cs`

csharp

```
namespace PLCHttpTester.Core.Models
{
    /// <summary>
    /// 词法单元
    /// </summary>
    public class Token
    {
        public TokenType Type { get; set; }
        public string Value { get; set; }

        public Token(TokenType type, string value)
        {
            Type = type;
            Value = value;
        }

        public override string ToString()
        {
            return $"{Type}: {Value}";
        }
    }

    /// <summary>
    /// 词法单元类型
    /// </summary>
    public enum TokenType
    {
        Variable,           // 变量名
        Operator,           // 操作符 = > < >= <= like
        LogicalOperator,    // 逻辑运算符 && ||
        Value,              // 值（字符串、数字、布尔）
        LeftParen,          // (
        RightParen,         // )
        End                 // 结束
    }
}
```

### 6.3 创建SuccessCriteriaParser - 第一部分（60分钟）

**步骤1：** 在 `Parsers` 文件夹下创建 `SuccessCriteriaParser.cs`

csharp

```
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using PLCHttpTester.Core.Models;

namespace PLCHttpTester.Core.Parsers
{
    /// <summary>
    /// 成功条件解析器
    /// 解析表达式如：$StatusCode=200&&Start_Success&&Start_TokenExpires>"2025-10-24"
    /// </summary>
    public class SuccessCriteriaParser
    {
        private List<Token> _tokens;
        private int _position;

        /// <summary>
        /// 解析表达式
        /// </summary>
        public ConditionNode Parse(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
                throw new ArgumentException("表达式不能为空");

            // 步骤1：词法分析
            _tokens = Tokenize(expression);
            _position = 0;

            // 步骤2：语法分析，构建表达式树
            ConditionNode root = ParseOrExpression();

            return root;
        }

        /// <summary>
        /// 词法分析 - 将表达式字符串分割为Token列表
        /// </summary>
        private List<Token> Tokenize(string expression)
        {
            var tokens = new List<Token>();
            int i = 0;

            while (i < expression.Length)
            {
                // 跳过空白字符
                if (char.IsWhiteSpace(expression[i]))
                {
                    i++;
                    continue;
                }

                // 识别逻辑运算符 && 和 ||
                if (i < expression.Length - 1)
                {
                    string twoChar = expression.Substring(i, 2);
                    if (twoChar == "&&" || twoChar == "||")
                    {
                        tokens.Add(new Token(TokenType.LogicalOperator, twoChar));
                        i += 2;
                        continue;
                    }
                }

                // 识别比较运算符 >= <= like
                if (i < expression.Length - 1)
                {
                    string twoChar = expression.Substring(i, 2);
                    if (twoChar == ">=" || twoChar == "<=")
                    {
                        tokens.Add(new Token(TokenType.Operator, twoChar));
                        i += 2;
                        continue;
                    }
                }

                // 识别 like（关键字）
                if (i < expression.Length - 4)
                {
                    string fourChar = expression.Substring(i, 4);
                    if (fourChar.ToLower() == "like")
                    {
                        tokens.Add(new Token(TokenType.Operator, "like"));
                        i += 4;
                        continue;
                    }
                }

                // 识别单字符运算符 = > 
                if (expression[i] == '=' || expression[i] == '>' || expression[i] == '<')
                {
                    tokens.Add(new Token(TokenType.Operator, expression[i].ToString()));
                    i++;
                    continue;
                }

                // 识别括号
                if (expression[i] == '(')
                {
                    tokens.Add(new Token(TokenType.LeftParen, "("));
                    i++;
                    continue;
                }

                if (expression[i] == ')')
                {
                    tokens.Add(new Token(TokenType.RightParen, ")"));
                    i++;
                    continue;
                }

                // 识别字符串值（带引号）
                if (expression[i] == '"' || expression[i] == '\'')
                {
                    char quote = expression[i];
                    int start = i + 1;
                    i++;
                    while (i < expression.Length && expression[i] != quote)
                    {
                        i++;
                    }
                    if (i < expression.Length)
                    {
                        string value = expression.Substring(start, i - start);
                        tokens.Add(new Token(TokenType.Value, value));
                        i++; // 跳过结束引号
                    }
                    continue;
                }

                // 识别变量名或值（数字、true、false）
                if (char.IsLetterOrDigit(expression[i]) || expression[i] == '$' || expression[i] == '_')
                {
                    int start = i;
                    while (i < expression.Length && 
                           (char.IsLetterOrDigit(expression[i]) || expression[i] == '_' || expression[i] == '$' || expression[i] == '.'))
                    {
                        i++;
                    }
                    string word = expression.Substring(start, i - start);

                    // 判断是变量还是值
                    if (word == "true" || word == "false" || IsNumeric(word))
                    {
                        tokens.Add(new Token(TokenType.Value, word));
                    }
                    else
                    {
                        tokens.Add(new Token(TokenType.Variable, word));
                    }
                    continue;
                }

                // 未识别的字符
                i++;
            }

            tokens.Add(new Token(TokenType.End, ""));
            return tokens;
        }

        private bool IsNumeric(string str)
        {
            return double.TryParse(str, out _);
        }

        // 语法分析部分在下一节继续...
    }
}
```

### 6.4 创建SuccessCriteriaParser - 第二部分（60分钟）

继续在 `SuccessCriteriaParser.cs` 中添加语法分析方法：

csharp

```
/// <summary>
        /// 解析OR表达式（最低优先级）
        /// </summary>
        private ConditionNode ParseOrExpression()
        {
            ConditionNode left = ParseAndExpression();

            while (CurrentToken().Type == TokenType.LogicalOperator && CurrentToken().Value == "||")
            {
                Consume(); // 消费 ||
                ConditionNode right = ParseAndExpression();
                left = new LogicalOperatorNode
                {
                    Operator = LogicalOperator.Or,
                    Left = left,
                    Right = right
                };
            }

            return left;
        }

        /// <summary>
        /// 解析AND表达式
        /// </summary>
        private ConditionNode ParseAndExpression()
        {
            ConditionNode left = ParseComparisonExpression();

            while (CurrentToken().Type == TokenType.LogicalOperator && CurrentToken().Value == "&&")
            {
                Consume(); // 消费 &&
                ConditionNode right = ParseComparisonExpression();
                left = new LogicalOperatorNode
                {
                    Operator = LogicalOperator.And,
                    Left = left,
                    Right = right
                };
            }

            return left;
        }

        /// <summary>
        /// 解析比较表达式或单独的布尔变量
        /// </summary>
        private ConditionNode ParseComparisonExpression()
        {
            // 处理括号
            if (CurrentToken().Type == TokenType.LeftParen)
            {
                Consume(); // 消费 (
                ConditionNode node = ParseOrExpression();
                if (CurrentToken().Type == TokenType.RightParen)
                {
                    Consume(); // 消费 )
                }
                return node;
            }

            // 必须是变量
            if (CurrentToken().Type != TokenType.Variable)
            {
                throw new Exception($"期望变量，但得到: {CurrentToken().Value}");
            }

            string variableName = CurrentToken().Value;
            Consume();

            // 检查是否有比较运算符
            if (CurrentToken().Type == TokenType.Operator)
            {
                string operatorStr = CurrentToken().Value;
                Consume();

                // 必须有值
                if (CurrentToken().Type != TokenType.Value)
                {
                    throw new Exception($"期望值，但得到: {CurrentToken().Value}");
                }

                string valueStr = CurrentToken().Value;
                Consume();

                // 创建比较节点
                return new ComparisonNode
                {
                    VariableName = variableName,
                    Operator = ParseComparisonOperator(operatorStr),
                    CompareValue = ParseValue(valueStr)
                };
            }
            else
            {
                // 没有运算符，作为布尔变量
                return new BooleanVariableNode
                {
                    VariableName = variableName
                };
            }
        }

        /// <summary>
        /// 解析比较运算符
        /// </summary>
        private ComparisonOperator ParseComparisonOperator(string op)
        {
            switch (op)
            {
                case "=":
                    return ComparisonOperator.Equal;
                case ">":
                    return ComparisonOperator.GreaterThan;
                case "<":
                    return ComparisonOperator.LessThan;
                case ">=":
                    return ComparisonOperator.GreaterOrEqual;
                case "<=":
                    return ComparisonOperator.LessOrEqual;
                case "like":
                    return ComparisonOperator.Like;
                default:
                    throw new Exception($"未知的比较运算符: {op}");
            }
        }

        /// <summary>
        /// 解析值
        /// </summary>
        private object ParseValue(string valueStr)
        {
            // 布尔值
            if (valueStr.ToLower() == "true")
                return true;
            if (valueStr.ToLower() == "false")
                return false;

            // 数字
            if (int.TryParse(valueStr, out int intValue))
                return intValue;
            if (double.TryParse(valueStr, out double doubleValue))
                return doubleValue;

            // 字符串
            return valueStr;
        }

        /// <summary>
        /// 获取当前Token
        /// </summary>
        private Token CurrentToken()
        {
            if (_position >= _tokens.Count)
                return _tokens[_tokens.Count - 1]; // 返回End token

            return _tokens[_position];
        }

        /// <summary>
        /// 消费当前Token，移动到下一个
        /// </summary>
        private void Consume()
        {
            if (_position < _tokens.Count - 1)
                _position++;
        }
```

### 6.5 测试成功条件解析器（30分钟）

**步骤1：** 在 `Form1.cs` 中添加测试方法

csharp

```
private void TestSuccessCriteriaParser()
{
    try
    {
        string expression = @"$StatusCode=200&&Start_Success&&Start_TokenExpires>""2025-10-24""&&Start_Token like ""token22%""";

        var parser = new SuccessCriteriaParser();
        var root = parser.Parse(expression);

        // 创建测试变量
        var variables = new Dictionary<string, Variable>();
        variables["$StatusCode"] = new Variable("$StatusCode", VariableType.Int, VariableSource.Response) { Value = 200 };
        variables["Start_Success"] = new Variable("Start_Success", VariableType.Bool, VariableSource.Response) { Value = true };
        variables["Start_TokenExpires"] = new Variable("Start_TokenExpires", VariableType.String, VariableSource.Response) { Value = "2025-10-25" };
        variables["Start_Token"] = new Variable("Start_Token", VariableType.String, VariableSource.Response) { Value = "token2233" };

        // 求值
        bool result = root.Evaluate(variables);

        MessageBox.Show($"表达式: {expression}\n\n解析树: {root}\n\n求值结果: {result}", "测试结果");
    }
    catch (Exception ex)
    {
        MessageBox.Show($"测试失败: {ex.Message}\n{ex.StackTrace}", "错误");
    }
}
```

**步骤2：** 修改构造函数调用测试

csharp

```
public Form1()
{
    InitializeComponent();
    TestSuccessCriteriaParser();
}
```

**步骤3：** 按 `F5` 运行测试

**预期结果：**

* 应该成功解析表达式
* 求值结果应该为 true
### 6.6 第五天总结检查

**完成情况检查清单：**

* ConditionNodes.cs 已创建（3个节点类）
* Token.cs 已创建
* SuccessCriteriaParser.cs 已创建
* 能正确进行词法分析
* 能正确构建表达式树
* 能正确求值
* 支持所有运算符（= > < >= <= like && ||）
* 测试通过
**第五天产出：**

* 4个模型类（条件节点和Token）
* 1个解析器类（成功条件）
* 完整的表达式解析和求值功能
---

## 7. 第六天：JSON处理器完善

### 7.1 添加JSON替换功能（40分钟）

**步骤1：** 在 `JsonProcessor.cs` 中添加方法

csharp

```
/// <summary>
        /// 在JSON模板中替换占位符
        /// </summary>
        public string ReplacePlaceholders(string jsonTemplate, Dictionary<string, string> replacements)
        {
            string result = jsonTemplate;

            foreach (var kvp in replacements)
            {
                string placeholder = StringHelper.CreatePlaceholder(kvp.Key);
                string value = kvp.Value;

                // 替换占位符
                result = result.Replace($"\"{placeholder}\"", value);
            }

            return result;
        }

        /// <summary>
        /// 格式化JSON字符串（美化输出）
        /// </summary>
        public string FormatJson(string json)
        {
            try
            {
                object obj = ParseJson(json);
                return ToJson(obj);
            }
            catch
            {
                return json;
            }
        }

        /// <summary>
        /// 验证JSON格式是否正确
        /// </summary>
        public bool IsValidJson(string json)
        {
            try
            {
                ParseJson(json);
                return true;
            }
            catch
            {
                return false;
            }
        }
```

### 7.2 创建类型转换工具（40分钟）

**步骤1：** 在 `Utils` 文件夹下创建 `TypeConverter.cs`

csharp

```
using System;
using PLCHttpTester.Core.Models;

namespace PLCHttpTester.Utils
{
    /// <summary>
    /// 类型转换工具
    /// </summary>
    public static class TypeConverter
    {
        /// <summary>
        /// 将JSON值转换为指定类型
        /// </summary>
        public static object ConvertFromJson(object jsonValue, VariableType targetType)
        {
            if (jsonValue == null)
                return GetDefaultValue(targetType);

            try
            {
                switch (targetType)
                {
                    case VariableType.Bool:
                        return Convert.ToBoolean(jsonValue);

                    case VariableType.Int:
                        return Convert.ToInt32(jsonValue);

                    case VariableType.Float:
                        return Convert.ToDouble(jsonValue);

                    case VariableType.String:
                        return jsonValue.ToString();

                    case VariableType.DateTime:
                        if (jsonValue is DateTime)
                            return jsonValue;
                        return DateTime.Parse(jsonValue.ToString());

                    default:
                        return jsonValue;
                }
            }
            catch
            {
                return GetDefaultValue(targetType);
            }
        }

        /// <summary>
        /// 将变量值转换为JSON字符串表示
        /// </summary>
        public static string ConvertToJsonString(object value, VariableType type)
        {
            if (value == null)
                return "null";

            switch (type)
            {
                case VariableType.Bool:
                    return value.ToString().ToLower();

                case VariableType.Int:
                case VariableType.Float:
                    return value.ToString();

                case VariableType.String:
                    // 转义特殊字符
                    string str = value.ToString();
                    str = str.Replace("\\", "\\\\")
                             .Replace("\"", "\\\"")
                             .Replace("\n", "\\n")
                             .Replace("\r", "\\r")
                             .Replace("\t", "\\t");
                    return $"\"{str}\"";

                case VariableType.DateTime:
                    return $"\"{value}\"";

                default:
                    return $"\"{value}\"";
            }
        }

        /// <summary>
        /// 获取类型的默认值
        /// </summary>
        public static object GetDefaultValue(VariableType type)
        {
            switch (type)
            {
                case VariableType.Bool:
                    return false;
                case VariableType.Int:
                    return 0;
                case VariableType.Float:
                    return 0.0;
                case VariableType.String:
                    return string.Empty;
                case VariableType.DateTime:
                    return DateTime.Now;
                default:
                    return null;
            }
        }

        /// <summary>
        /// 尝试解析字符串为指定类型
        /// </summary>
        public static bool TryParse(string valueString, VariableType type, out object result)
        {
            result = null;

            try
            {
                switch (type)
                {
                    case VariableType.Bool:
                        result = bool.Parse(valueString);
                        return true;

                    case VariableType.Int:
                        result = int.Parse(valueString);
                        return true;

                    case VariableType.Float:
                        result = double.Parse(valueString);
                        return true;

                    case VariableType.String:
                        result = valueString;
                        return true;

                    case VariableType.DateTime:
                        result = DateTime.Parse(valueString);
                        return true;

                    default:
                        return false;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
```

### 7.3 测试JSON处理功能（30分钟）

**步骤1：** 在 `Form1.cs` 中添加测试

csharp

```
private void TestJsonProcessor()
{
    try
    {
        var jsonProcessor = new JsonProcessor();

        // 测试1：占位符替换
        string template = @"{
    ""name"": ""${VAR_1}$"",
    ""age"": ""${VAR_2}$"",
    ""active"": ""${VAR_3}$""
}";

        var replacements = new Dictionary<string, string>
        {
            { "VAR_1", "\"John\"" },
            { "VAR_2", "25" },
            { "VAR_3", "true" }
        };

        string replaced = jsonProcessor.ReplacePlaceholders(template, replacements);

        // 测试2：JSON Pointer
        object jsonObj = jsonProcessor.ParseJson(replaced);
        object nameValue = jsonProcessor.GetValueByPointer(jsonObj, "/name");

        // 测试3：遍历
        var paths = new List<string>();
        jsonProcessor.TraverseJson(jsonObj, "", (path, value) =>
        {
            paths.Add($"{path} = {value}");
        });

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("替换后的JSON:");
        sb.AppendLine(replaced);
        sb.AppendLine();
        sb.AppendLine($"name字段值: {nameValue}");
        sb.AppendLine();
        sb.AppendLine("所有路径:");
        foreach (var path in paths)
        {
            sb.AppendLine(path);
        }

        MessageBox.Show(sb.ToString(), "JSON处理测试");
    }
    catch (Exception ex)
    {
        MessageBox.Show($"测试失败: {ex.Message}\n{ex.StackTrace}", "错误");
    }
}
```

### 7.4 第六天总结检查

**完成情况检查清单：**

* JSON占位符替换功能完成
* JSON格式化功能完成
* 类型转换工具完成
* 所有功能测试通过
**第六天产出：**

* 完善的JSON处理器
* 类型转换工具类
* 完整的JSON处理能力
---

## 8. 第七天：HTTP请求处理器

### 8.1 创建HTTP响应数据类（20分钟）

**步骤1：** 在 `Models` 文件夹下创建 `HttpResponseData.cs`

csharp

```
using System.Collections.Generic;

namespace PLCHttpTester.Core.Models
{
    /// <summary>
    /// HTTP响应数据
    /// </summary>
    public class HttpResponseData
    {
        /// <summary>
        /// 状态码
        /// </summary>
        public int StatusCode { get; set; }

        /// <summary>
        /// 状态消息
        /// </summary>
        public string StatusMessage { get; set; }

        /// <summary>
        /// 响应头
        /// </summary>
        public Dictionary<string, string> Headers { get; set; }

        /// <summary>
        /// 响应Body
        /// </summary>
        public string Body { get; set; }

        /// <summary>
        /// 是否成功
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        public string ErrorMessage { get; set; }

        public HttpResponseData()
        {
            Headers = new Dictionary<string, string>();
        }
    }
}
```

### 8.2 创建HttpRequestProcessor（70分钟）

**步骤1：** 在 `Processors` 文件夹下创建 `HttpRequestProcessor.cs`

csharp

```
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using PLCHttpTester.Core.Models;
using PLCHttpTester.Utils;

namespace PLCHttpTester.Core.Processors
{
    /// <summary>
    /// HTTP请求处理器
    /// </summary>
    public class HttpRequestProcessor
    {
        private JsonProcessor _jsonProcessor;

        public HttpRequestProcessor()
        {
            _jsonProcessor = new JsonProcessor();
        }

        /// <summary>
        /// 构建真实的HTTP请求
        /// </summary>
        public string BuildRequest(
            HttpRequestTemplate template,
            Dictionary<string, Variable> variables)
        {
            StringBuilder requestBuilder = new StringBuilder();

            // 步骤1：处理URL，替换变量
            string processedUrl = ProcessUrl(template.Url, template.Expressions, variables);

            // 步骤2：构建请求行
            requestBuilder.AppendLine($"{template.Method} {processedUrl}");

            // 步骤3：处理Headers，替换变量
            foreach (var header in template.Headers)
            {
                string processedValue = ProcessHeaderValue(header.Value, template.Expressions, variables);
                requestBuilder.AppendLine($"{header.Key}: {processedValue}");
            }

            // 步骤4：处理Body
            if (!string.IsNullOrWhiteSpace(template.BodyTemplate))
            {
                requestBuilder.AppendLine(); // 空行分隔
                string processedBody = ProcessBody(template.BodyTemplate, template.Expressions, variables);
                requestBuilder.Append(processedBody);
            }

            return requestBuilder.ToString();
        }

        /// <summary>
        /// 处理URL中的变量
        /// </summary>
        private string ProcessUrl(string url, List<TemplateExpression> expressions, Dictionary<string, Variable> variables)
        {
            string result = url;

            // 找出URL中的所有表达式
            var urlExpressions = expressions.FindAll(e => e.Location == ExpressionLocation.Url);

            foreach (var expression in urlExpressions)
            {
                if (variables.ContainsKey(expression.VariableName))
                {
                    Variable variable = variables[expression.VariableName];
                    string value = variable.GetFormattedValue();
                    result = result.Replace(expression.OriginalText, value);
                }
            }

            return result;
        }

        /// <summary>
        /// 处理Header值中的变量
        /// </summary>
        private string ProcessHeaderValue(string headerValue, List<TemplateExpression> expressions, Dictionary<string, Variable> variables)
        {
            string result = headerValue;

            var headerExpressions = expressions.FindAll(e => e.Location == ExpressionLocation.Header);

            foreach (var expression in headerExpressions)
            {
                if (headerValue.Contains(expression.OriginalText))
                {
                    if (variables.ContainsKey(expression.VariableName))
                    {
                        Variable variable = variables[expression.VariableName];
                        string value = variable.GetFormattedValue();
                        result = result.Replace(expression.OriginalText, value);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 处理Body，替换占位符
        /// </summary>
        private string ProcessBody(string bodyTemplate, List<TemplateExpression> expressions, Dictionary<string, Variable> variables)
        {
            // 构建替换字典
            var replacements = new Dictionary<string, string>();

            var bodyExpressions = expressions.FindAll(e => e.Location == ExpressionLocation.Body);

            foreach (var expression in bodyExpressions)
            {
                if (variables.ContainsKey(expression.VariableName))
                {
                    Variable variable = variables[expression.VariableName];
                    string jsonValue = TypeConverter.ConvertToJsonString(variable.Value, variable.Type);
                    replacements[expression.Id] = jsonValue;
                }
                else
                {
                    // 变量不存在，使用默认值
                    object defaultValue = TypeConverter.GetDefaultValue(expression.DataType.Value);
                    string jsonValue = TypeConverter.ConvertToJsonString(defaultValue, expression.DataType.Value);
                    replacements[expression.Id] = jsonValue;
                }
            }

            // 替换占位符
            return _jsonProcessor.ReplacePlaceholders(bodyTemplate, replacements);
        }

        /// <summary>
        /// 发送HTTP请求
        /// </summary>
        public async Task<HttpResponseData> SendRequestAsync(
            string baseUrl,
            string method,
            string path,
            Dictionary<string, string> headers,
            string body)
        {
            var response = new HttpResponseData();

            try
            {
                // 构建完整URL
                string fullUrl = baseUrl.TrimEnd('/') + path;

                // 创建请求
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(fullUrl);
                request.Method = method;
                request.Timeout = 30000; // 30秒超时

                // 设置Headers
                foreach (var header in headers)
                {
                    // 某些Header需要特殊处理
                    switch (header.Key.ToLower())
                    {
                        case "content-type":
                            request.ContentType = header.Value;
                            break;
                        case "user-agent":
                            request.UserAgent = header.Value;
                            break;
                        case "accept":
                            request.Accept = header.Value;
                            break;
                        default:
                            request.Headers.Add(header.Key, header.Value);
                            break;
                    }
                }

                // 发送Body（如果有）
                if (!string.IsNullOrEmpty(body) && (method == "POST" || method == "PUT" || method == "PATCH"))
                {
                    byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
                    request.ContentLength = bodyBytes.Length;

                    using (Stream requestStream = await request.GetRequestStreamAsync())
                    {
                        await requestStream.WriteAsync(bodyBytes, 0, bodyBytes.Length);
                    }
                }

                // 获取响应
                using (HttpWebResponse webResponse = (HttpWebResponse)await request.GetResponseAsync())
                {
                    response.StatusCode = (int)webResponse.StatusCode;
                    response.StatusMessage = webResponse.StatusDescription;
                    response.IsSuccess = true;

                    // 读取响应头
                    foreach (string key in webResponse.Headers.AllKeys)
                    {
                        response.Headers[key] = webResponse.Headers[key];
                    }

                    // 读取响应Body
                    using (StreamReader reader = new StreamReader(webResponse.GetResponseStream(), Encoding.UTF8))
                    {
                        response.Body = await reader.ReadToEndAsync();
                    }
                }
            }
            catch (WebException ex)
            {
                if (ex.Response != null)
                {
                    HttpWebResponse errorResponse = (HttpWebResponse)ex.Response;
                    response.StatusCode = (int)errorResponse.StatusCode;
                    response.StatusMessage = errorResponse.StatusDescription;
                    response.IsSuccess = false;
                    response.ErrorMessage = ex.Message;

                    // 尝试读取错误响应的Body
                    try
                    {
                        using (StreamReader reader = new StreamReader(errorResponse.GetResponseStream(), Encoding.UTF8))
                        {
                            response.Body = reader.ReadToEnd();
                        }
                    }
                    catch
                    {
                        response.Body = string.Empty;
                    }
                }
                else
                {
                    response.IsSuccess = false;
                    response.ErrorMessage = $"请求失败: {ex.Message}";
                }
            }
            catch (Exception ex)
            {
                response.IsSuccess = false;
                response.ErrorMessage = $"发生错误: {ex.Message}";
            }

            return response;
        }
    }
}
```

### 8.3 测试HTTP请求构建（30分钟）

**步骤1：** 在 `Form1.cs` 中添加测试

csharp

```
private void TestHttpRequestProcessor()
{
    try
    {
        // 步骤1：创建请求模板
        string templateText = @"POST /api/mes/start?stationId=@(StationID:d)
User-Agent: TestAgent
Content-Type: application/json

{
""start_time"": @String(CurrentTime:yyyy-MM-dd),
""count"": @Number(Count)
}";

        var requestParser = new RequestTemplateParser();
        var template = requestParser.Parse(templateText);

        // 步骤2：创建变量管理器并设置变量值
        var varManager = new VariableManager();
        
        foreach (var expr in template.Expressions)
        {
            var variable = new Variable(expr.VariableName, expr.DataType ?? VariableType.String, VariableSource.Request, expr.FormatString);
            varManager.RegisterVariable(variable);
        }

        // 设置变量值
        varManager.SetVariableValue("StationID", 123);
        varManager.SetVariableValue("CurrentTime", DateTime.Now);
        varManager.SetVariableValue("Count", 5);

        // 步骤3：构建请求
        var processor = new HttpRequestProcessor();
        string realRequest = processor.BuildRequest(template, varManager.GetAllVariables());

        MessageBox.Show(realRequest, "生成的HTTP请求");
    }
    catch (Exception ex)
    {
        MessageBox.Show($"测试失败: {ex.Message}\n{ex.StackTrace}", "错误");
    }
}
```

### 8.4 第七天总结检查

**完成情况检查清单：**

* HttpResponseData.cs 已创建
* HttpRequestProcessor.cs 已创建
* 能正确替换URL中的变量
* 能正确替换Header中的变量
* 能正确替换Body中的变量
* 能正确发送HTTP请求
* 测试通过
**第七天产出：**

* 1个数据类
* 1个处理器类
* 完整的HTTP请求构建和发送功能
---

## 9. 第八天：HTTP响应处理器

### 9.1 创建HttpResponseProcessor（80分钟）

**步骤1：** 在 `Processors` 文件夹下创建 `HttpResponseProcessor.cs`

csharp

```
using System;
using System.Collections.Generic;
using PLCHttpTester.Core.Models;
using PLCHttpTester.Utils;

namespace PLCHttpTester.Core.Processors
{
    /// <summary>
    /// HTTP响应处理器
    /// </summary>
    public class HttpResponseProcessor
    {
        private JsonProcessor _jsonProcessor;

        public HttpResponseProcessor()
        {
            _jsonProcessor = new JsonProcessor();
        }

        /// <summary>
        /// 处理HTTP响应，提取变量值
        /// </summary>
        public void ProcessResponse(
            HttpResponseData response,
            HttpResponseTemplate template,
            Dictionary<string, Variable> variables)
        {
            if (response == null || template == null)
                return;

            // 步骤1：添加特殊变量 $StatusCode
            if (!variables.ContainsKey("$StatusCode"))
            {
                variables["$StatusCode"] = new Variable("$StatusCode", VariableType.Int, VariableSource.Response);
            }
            variables["$StatusCode"].Value = response.StatusCode;

            // 步骤2：解析响应Body JSON
            if (string.IsNullOrWhiteSpace(response.Body))
                return;

            try
            {
                object jsonObj = _jsonProcessor.ParseJson(response.Body);

                // 步骤3：根据ResponseMapping提取变量值
                foreach (var mapping in template.Mappings)
                {
                    ExtractVariable(jsonObj, mapping, variables);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"响应JSON解析失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 根据映射提取变量值
        /// </summary>
        private void ExtractVariable(
            object jsonRoot,
            ResponseMapping mapping,
            Dictionary<string, Variable> variables)
        {
            try
            {
                // 使用JSON Pointer获取值
                object value = _jsonProcessor.GetValueByPointer(jsonRoot, mapping.
```

EX

Continue

JsonPointer);

```
if (value == null)
            {
                // 路径不存在，使用默认值
                SetVariableDefaultValue(mapping.VariableName, mapping.DataType, variables);
                return;
            }

            // 类型转换
            object convertedValue = TypeConverter.ConvertFromJson(value, mapping.DataType);

            // 更新或创建变量
            if (variables.ContainsKey(mapping.VariableName))
            {
                variables[mapping.VariableName].Value = convertedValue;
            }
            else
            {
                var variable = new Variable(mapping.VariableName, mapping.DataType, VariableSource.Response)
                {
                    Value = convertedValue
                };
                variables[mapping.VariableName] = variable;
            }
        }
        catch (Exception ex)
        {
            // 提取失败，使用默认值
            SetVariableDefaultValue(mapping.VariableName, mapping.DataType, variables);
            Console.WriteLine($"提取变量 {mapping.VariableName} 失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 设置变量默认值
    /// </summary>
    private void SetVariableDefaultValue(
        string variableName,
        VariableType dataType,
        Dictionary<string, Variable> variables)
    {
        object defaultValue = TypeConverter.GetDefaultValue(dataType);

        if (variables.ContainsKey(variableName))
        {
            variables[variableName].Value = defaultValue;
        }
        else
        {
            var variable = new Variable(variableName, dataType, VariableSource.Response)
            {
                Value = defaultValue
            };
            variables[variableName] = variable;
        }
    }

    /// <summary>
    /// 验证响应是否符合模板
    /// </summary>
    public bool ValidateResponse(HttpResponseData response, HttpResponseTemplate template)
    {
        // 检查状态码
        if (template.ExpectedStatusCode.HasValue)
        {
            if (response.StatusCode != template.ExpectedStatusCode.Value)
                return false;
        }

        // 检查是否有Body
        if (template.Mappings.Count > 0 && string.IsNullOrWhiteSpace(response.Body))
            return false;

        return true;
    }

    /// <summary>
    /// 格式化响应信息（用于显示）
    /// </summary>
    public string FormatResponse(HttpResponseData response)
    {
        if (response == null)
            return string.Empty;

        var sb = new System.Text.StringBuilder();

        // 状态行
        sb.AppendLine($"{response.StatusCode} {response.StatusMessage}");
        sb.AppendLine();

        // Headers
        if (response.Headers.Count > 0)
        {
            sb.AppendLine("Headers:");
            foreach (var header in response.Headers)
            {
                sb.AppendLine($"  {header.Key}: {header.Value}");
            }
            sb.AppendLine();
        }

        // Body
        if (!string.IsNullOrWhiteSpace(response.Body))
        {
            sb.AppendLine("Body:");
            // 尝试格式化JSON
            string formattedBody = _jsonProcessor.FormatJson(response.Body);
            sb.AppendLine(formattedBody);
        }

        // 错误信息
        if (!string.IsNullOrWhiteSpace(response.ErrorMessage))
        {
            sb.AppendLine();
            sb.AppendLine($"Error: {response.ErrorMessage}");
        }

        return sb.ToString();
    }
}
```

}

```
### 9.2 测试响应处理器（40分钟）

**步骤1：** 在 `Form1.cs` 中添加测试
```csharp
private void TestHttpResponseProcessor()
{
    try
    {
        // 步骤1：创建响应模板
        string templateText = @"200 OK 
Content-Type: application/json

{
    ""success"": @Bool(Start_Success),
    ""message"": @String(Start_Message),
    ""ticket"": {
        ""token"": @Number(Start_Token),
        ""expires"": @String(Start_TokenExpires)
    }
}";

        var responseParser = new ResponseTemplateParser();
        var template = responseParser.Parse(templateText);

        // 步骤2：创建模拟的HTTP响应
        var response = new HttpResponseData
        {
            StatusCode = 200,
            StatusMessage = "OK",
            Body = @"{
    ""success"": true,
    ""message"": ""Operation completed"",
    ""ticket"": {
        ""token"": 12345,
        ""expires"": ""2025-12-31""
    }
}"
        };

        // 步骤3：处理响应
        var variables = new Dictionary<string, Variable>();
        var processor = new HttpResponseProcessor();
        processor.ProcessResponse(response, template, variables);

        // 步骤4：显示提取的变量
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("提取的变量:");
        foreach (var kvp in variables)
        {
            sb.AppendLine($"  {kvp.Key} = {kvp.Value.Value} ({kvp.Value.Type})");
        }

        MessageBox.Show(sb.ToString(), "响应处理测试");
    }
    catch (Exception ex)
    {
        MessageBox.Show($"测试失败: {ex.Message}\n{ex.StackTrace}", "错误");
    }
}
```

### 9.3 第八天总结检查

**完成情况检查清单：**
- [ ] HttpResponseProcessor.cs 已创建
- [ ] 能正确解析响应JSON
- [ ] 能正确提取变量值
- [ ] 能正确处理类型转换
- [ ] 能正确处理不存在的字段（默认值）
- [ ] 测试通过

**第八天产出：**
- 1个处理器类
- 完整的响应处理功能

---

## 10. 第九天：业务服务层

### 10.1 创建TestResult类（20分钟）

**步骤1：** 在 `Models` 文件夹下创建 `TestResult.cs`
```csharp
using System;
using System.Collections.Generic;

namespace PLCHttpTester.Core.Models
{
    /// <summary>
    /// 测试结果
    /// </summary>
    public class TestResult
    {
        /// <summary>
        /// 测试是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// HTTP状态码
        /// </summary>
        public int StatusCode { get; set; }

        /// <summary>
        /// 实际发送的请求文本
        /// </summary>
        public string RequestText { get; set; }

        /// <summary>
        /// 收到的响应文本
        /// </summary>
        public string ResponseText { get; set; }

        /// <summary>
        /// 提取的变量
        /// </summary>
        public Dictionary<string, Variable> ExtractedVariables { get; set; }

        /// <summary>
        /// 成功条件评估结果
        /// </summary>
        public bool? SuccessCriteriaResult { get; set; }

        /// <summary>
        /// 成功条件评估详情
        /// </summary>
        public string SuccessCriteriaDetail { get; set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// 执行时间
        /// </summary>
        public DateTime ExecutionTime { get; set; }

        /// <summary>
        /// 执行耗时（毫秒）
        /// </summary>
        public long DurationMs { get; set; }

        public TestResult()
        {
            ExtractedVariables = new Dictionary<string, Variable>();
            ExecutionTime = DateTime.Now;
        }
    }
}
```

### 10.2 创建HttpTestService - 第一部分（60分钟）

**步骤1：** 在 `Services` 文件夹下创建 `HttpTestService.cs`
```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using PLCHttpTester.Core.Models;
using PLCHttpTester.Core.Parsers;
using PLCHttpTester.Core.Processors;

namespace PLCHttpTester.Core.Services
{
    /// <summary>
    /// HTTP测试服务 - 主要业务逻辑
    /// </summary>
    public class HttpTestService
    {
        // 解析器
        private RequestTemplateParser _requestParser;
        private ResponseTemplateParser _responseParser;
        private SuccessCriteriaParser _criteriaParser;

        // 处理器
        private HttpRequestProcessor _requestProcessor;
        private HttpResponseProcessor _responseProcessor;

        // 管理器
        private VariableManager _variableManager;

        // 模板
        private HttpRequestTemplate _requestTemplate;
        private HttpResponseTemplate _responseTemplate;
        private ConditionNode _successCriteria;

        // 配置
        private string _baseUrl;

        public HttpTestService()
        {
            _requestParser = new RequestTemplateParser();
            _responseParser = new ResponseTemplateParser();
            _criteriaParser = new SuccessCriteriaParser();

            _requestProcessor = new HttpRequestProcessor();
            _responseProcessor = new HttpResponseProcessor();

            _variableManager = new VariableManager();
        }

        /// <summary>
        /// 设置基础URL
        /// </summary>
        public void SetBaseUrl(string baseUrl)
        {
            _baseUrl = baseUrl;
        }

        /// <summary>
        /// 加载请求模板
        /// </summary>
        public void LoadRequestTemplate(string templateText)
        {
            try
            {
                // 解析模板
                _requestTemplate = _requestParser.Parse(templateText);

                // 注册所有请求变量
                foreach (var expression in _requestTemplate.Expressions)
                {
                    if (!string.IsNullOrEmpty(expression.VariableName))
                    {
                        // 确定数据类型
                        VariableType varType = expression.DataType ?? VariableType.String;

                        var variable = new Variable(
                            expression.VariableName,
                            varType,
                            VariableSource.Request,
                            expression.FormatString);

                        _variableManager.RegisterVariable(variable);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"请求模板加载失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 加载响应模板
        /// </summary>
        public void LoadResponseTemplate(string templateText)
        {
            try
            {
                // 解析模板
                _responseTemplate = _responseParser.Parse(templateText);

                // 注册所有响应变量
                foreach (var mapping in _responseTemplate.Mappings)
                {
                    var variable = new Variable(
                        mapping.VariableName,
                        mapping.DataType,
                        VariableSource.Response);

                    _variableManager.RegisterVariable(variable);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"响应模板加载失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 加载成功条件
        /// </summary>
        public void LoadSuccessCriteria(string expression)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(expression))
                {
                    _successCriteria = _criteriaParser.Parse(expression);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"成功条件加载失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 获取变量管理器
        /// </summary>
        public VariableManager GetVariableManager()
        {
            return _variableManager;
        }

        /// <summary>
        /// 验证是否可以执行测试
        /// </summary>
        public ValidationResult ValidateBeforeExecution()
        {
            var result = new ValidationResult { IsValid = true };

            // 检查是否已加载请求模板
            if (_requestTemplate == null)
            {
                result.IsValid = false;
                result.ErrorMessages.Add("未加载请求模板");
            }

            // 检查是否已加载响应模板
            if (_responseTemplate == null)
            {
                result.IsValid = false;
                result.ErrorMessages.Add("未加载响应模板");
            }

            // 检查基础URL
            if (string.IsNullOrWhiteSpace(_baseUrl))
            {
                result.IsValid = false;
                result.ErrorMessages.Add("未设置基础URL");
            }

            // 检查所有请求变量是否已赋值
            if (!_variableManager.AreAllRequestVariablesSet())
            {
                result.IsValid = false;
                var unsetVars = _variableManager.GetUnsetVariableNames();
                result.ErrorMessages.Add($"以下变量未赋值: {string.Join(", ", unsetVars)}");
            }

            return result;
        }

        // 继续在下一节...
    }

    /// <summary>
    /// 验证结果
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> ErrorMessages { get; set; }

        public ValidationResult()
        {
            ErrorMessages = new List<string>();
        }

        public string GetErrorMessage()
        {
            return string.Join("\n", ErrorMessages);
        }
    }
}
```

### 10.3 创建HttpTestService - 第二部分（60分钟）

继续在 `HttpTestService.cs` 中添加执行测试的方法：
```csharp
        /// <summary>
        /// 执行测试
        /// </summary>
        public async Task<TestResult> ExecuteTestAsync()
        {
            var result = new TestResult();
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // 步骤1：验证
                var validation = ValidateBeforeExecution();
                if (!validation.IsValid)
                {
                    result.Success = false;
                    result.ErrorMessage = validation.GetErrorMessage();
                    return result;
                }

                // 步骤2：构建请求
                string requestText = _requestProcessor.BuildRequest(
                    _requestTemplate,
                    _variableManager.GetAllVariables());

                result.RequestText = requestText;

                // 步骤3：发送HTTP请求
                var response = await _requestProcessor.SendRequestAsync(
                    _baseUrl,
                    _requestTemplate.Method,
                    ExtractPath(_requestTemplate.Url),
                    _requestTemplate.Headers,
                    ExtractBody(requestText));

                result.StatusCode = response.StatusCode;
                result.ResponseText = _responseProcessor.FormatResponse(response);

                // 步骤4：检查HTTP请求是否成功
                if (!response.IsSuccess)
                {
                    result.Success = false;
                    result.ErrorMessage = response.ErrorMessage;
                    stopwatch.Stop();
                    result.DurationMs = stopwatch.ElapsedMilliseconds;
                    return result;
                }

                // 步骤5：处理响应，提取变量
                _responseProcessor.ProcessResponse(
                    response,
                    _responseTemplate,
                    _variableManager.GetAllVariables());

                result.ExtractedVariables = new Dictionary<string, Variable>(_variableManager.GetAllVariables());

                // 步骤6：评估成功条件
                if (_successCriteria != null)
                {
                    bool criteriaResult = _successCriteria.Evaluate(_variableManager.GetAllVariables());
                    result.SuccessCriteriaResult = criteriaResult;
                    result.SuccessCriteriaDetail = BuildCriteriaDetail(criteriaResult);
                    result.Success = criteriaResult;
                }
                else
                {
                    // 没有设置成功条件，仅基于HTTP状态码判断
                    result.Success = (response.StatusCode >= 200 && response.StatusCode < 300);
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"执行测试时发生错误: {ex.Message}";
            }
            finally
            {
                stopwatch.Stop();
                result.DurationMs = stopwatch.ElapsedMilliseconds;
            }

            return result;
        }

        /// <summary>
        /// 从URL中提取路径部分
        /// </summary>
        private string ExtractPath(string url)
        {
            // URL格式：/api/mes/start?param=value
            // 已经是路径格式
            return url;
        }

        /// <summary>
        /// 从请求文本中提取Body
        /// </summary>
        private string ExtractBody(string requestText)
        {
            // 按空行分割
            string[] parts = requestText.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.None);
            if (parts.Length > 1)
            {
                return parts[1].Trim();
            }
            return string.Empty;
        }

        /// <summary>
        /// 构建条件评估详情
        /// </summary>
        private string BuildCriteriaDetail(bool result)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"成功条件评估结果: {(result ? "通过" : "失败")}");
            sb.AppendLine();
            sb.AppendLine("变量值:");

            foreach (var variable in _variableManager.GetAllVariables().Values)
            {
                sb.AppendLine($"  {variable.Name} = {variable.Value} ({variable.Type})");
            }

            return sb.ToString();
        }

        /// <summary>
        /// 重置服务（清除所有数据）
        /// </summary>
        public void Reset()
        {
            _requestTemplate = null;
            _responseTemplate = null;
            _successCriteria = null;
            _variableManager.Clear();
        }

        /// <summary>
        /// 获取请求模板
        /// </summary>
        public HttpRequestTemplate GetRequestTemplate()
        {
            return _requestTemplate;
        }

        /// <summary>
        /// 获取响应模板
        /// </summary>
        public HttpResponseTemplate GetResponseTemplate()
        {
            return _responseTemplate;
        }
```

### 10.4 测试完整流程（40分钟）

**步骤1：** 在 `Form1.cs` 中添加完整测试
```csharp
private async void TestCompleteFlow()
{
    try
    {
        var service = new HttpTestService();

        // 1. 设置基础URL
        service.SetBaseUrl("http://httpbin.org");  // 使用httpbin作为测试

        // 2. 加载请求模板
        string requestTemplate = @"POST /post
User-Agent: TestAgent
Content-Type: application/json

{
    ""name"": @String(UserName),
    ""age"": @Number(UserAge),
    ""active"": @Bool(IsActive)
}";
        service.LoadRequestTemplate(requestTemplate);

        // 3. 加载响应模板（httpbin会返回我们发送的数据）
        string responseTemplate = @"200 OK
Content-Type: application/json

{
    ""json"": {
        ""name"": @String(ReturnedName),
        ""age"": @Number(ReturnedAge)
    }
}";
        service.LoadResponseTemplate(responseTemplate);

        // 4. 设置变量值
        var varManager = service.GetVariableManager();
        varManager.SetVariableValue("UserName", "张三");
        varManager.SetVariableValue("UserAge", 25);
        varManager.SetVariableValue("IsActive", true);

        // 5. 设置成功条件
        service.LoadSuccessCriteria("$StatusCode=200");

        // 6. 执行测试
        var result = await service.ExecuteTestAsync();

        // 7. 显示结果
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"测试结果: {(result.Success ? "成功" : "失败")}");
        sb.AppendLine($"状态码: {result.StatusCode}");
        sb.AppendLine($"耗时: {result.DurationMs}ms");
        sb.AppendLine();
        sb.AppendLine("发送的请求:");
        sb.AppendLine(result.RequestText);
        sb.AppendLine();
        sb.AppendLine("收到的响应:");
        sb.AppendLine(result.ResponseText);

        if (!string.IsNullOrEmpty(result.ErrorMessage))
        {
            sb.AppendLine();
            sb.AppendLine($"错误: {result.ErrorMessage}");
        }

        MessageBox.Show(sb.ToString(), "完整流程测试");
    }
    catch (Exception ex)
    {
        MessageBox.Show($"测试失败: {ex.Message}\n{ex.StackTrace}", "错误");
    }
}
```

**步骤2：** 修改构造函数
```csharp
public Form1()
{
    InitializeComponent();
    TestCompleteFlow();
}
```

**步骤3：** 按 `F5` 运行测试

### 10.5 第九天总结检查

**完成情况检查清单：**
- [ ] TestResult.cs 已创建
- [ ] HttpTestService.cs 已创建
- [ ] 能正确加载模板
- [ ] 能正确管理变量
- [ ] 能正确执行完整流程
- [ ] 能正确返回测试结果
- [ ] 完整流程测试通过

**第九天产出：**
- 2个类（TestResult和HttpTestService）
- 完整的业务逻辑层
- 所有核心功能集成完成

---

## 11. 第十天：主界面设计

### 11.1 设计主窗体布局（60分钟）

**步骤1：** 打开 `Form1.cs` 的设计器（双击 `Form1.cs` 或右键 -> 查看设计器）

**步骤2：** 设置窗体属性
- Name: `MainForm`
- Text: `PLC-MES HTTP测试工具`
- Size: `1200, 800`
- StartPosition: `CenterScreen`

**步骤3：** 添加主容器控件

从工具箱拖拽以下控件到窗体：

1. **SplitContainer** (名称: `splitMain`)
   - Dock: Fill
   - Orientation: Horizontal
   - SplitterDistance: 450

2. 在 `splitMain.Panel1` 中添加 **TabControl** (名称: `tabTemplates`)
   - Dock: Fill
   - 添加3个TabPage：
     - tabPageRequest (Text: "请求模板")
     - tabPageResponse (Text: "响应模板")
     - tabPageCriteria (Text: "成功条件")

3. 在 `splitMain.Panel2` 中添加 **TabControl** (名称: `tabResults`)
   - Dock: Fill
   - 添加2个TabPage：
     - tabPageVariables (Text: "变量管理")
     - tabPageResult (Text: "执行结果")

### 11.2 设计请求模板标签页（40分钟）

**步骤1：** 选中 `tabPageRequest`

**步骤2：** 添加控件：

1. **Panel** (Dock: Top, Height: 40)
   - 添加 **Label**: Text = "基础URL:"
   - 添加 **TextBox** (名称: `txtBaseUrl`): Width = 300, Text = "http://localhost:8080"
   
2. **TextBox** (名称: `txtRequestTemplate`)
   - Dock: Fill
   - Multiline: True
   - ScrollBars: Both
   - Font: Consolas, 10pt
   - 默认文本:
```
POST /api/mes/start?stationId=@(StationID:d)
User-Agent: BYD_PLC2MES
Content-Type: application/json

{
    "start_time": @String(CurrentTime:yyyy-MM-dd HH:mm:ss.fff),
    "sn": @String(SerialNo),
    "count": @Number(Count),
    "done": @Bool(Done)
}
```

### 11.3 设计响应模板标签页（30分钟）

**步骤1：** 选中 `tabPageResponse`

**步骤2：** 添加控件：

1. **TextBox** (名称: `txtResponseTemplate`)
   - Dock: Fill
   - Multiline: True
   - ScrollBars: Both
   - Font: Consolas, 10pt
   - 默认文本:
```
200 OK 
Content-Type: application/json

{
    "success": @Bool(Start_Success),
    "message": @String(Start_Message),
    "ticket": {
        "token": @Number(Start_Token),
        "expires": @String(Start_TokenExpires)
    }
}
```

### 11.4 设计成功条件标签页（30分钟）

**步骤1：** 选中 `tabPageCriteria`

**步骤2：** 添加控件：

1. **Label** (Dock: Top)
   - Text: "成功判断表达式（支持 && || = >= <= > < like）:"
   - Height: 30

2. **TextBox** (名称: `txtSuccessCriteria`)
   - Dock: Fill
   - Multiline: True
   - ScrollBars: Both
   - Font: Consolas, 10pt
   - 默认文本:
```
$StatusCode=200&&Start_Success&&Start_TokenExpires>"2025-10-24"
```

### 11.5 设计变量管理标签页（50分钟）

**步骤1：** 选中 `tabPageVariables`

**步骤2：** 添加控件：

1. **Panel** (Dock: Top, Height: 40)
   - 添加 **Button** (名称: `btnParseTemplates`): Text = "解析模板", Size = (100, 30)
   - 添加 **Button** (名称: `btnSendRequest`): Text = "发送请求", Size = (100, 30)

2. **GroupBox** (名称: `groupRequestVars`, Dock: Top, Height: 250)
   - Text: "请求变量（需要输入值）"
   - 添加 **DataGridView** (名称: `dgvRequestVariables`)
     - Dock: Fill
     - AllowUserToAddRows: False
     - AllowUserToDeleteRows: False
     - SelectionMode: FullRowSelect
     - 列设置：
       - VariableName (只读): "变量名"
       - VariableType (只读): "类型"
       - VariableValue (可编辑): "值"
       - FormatString (只读): "格式"

3. **GroupBox** (名称: `groupResponseVars`, Dock: Fill)
   - Text: "响应变量（自动提取）"
   - 添加 **DataGridView** (名称: `dgvResponseVariables`)
     - Dock: Fill
     - AllowUserToAddRows: False
     - AllowUserToDeleteRows: False
     - ReadOnly: True
     - 列设置：
       - VariableName: "变量名"
       - VariableType: "类型"
       - VariableValue: "值"
       - JsonPath: "JSON路径"

### 11.6 设计执行结果标签页（40分钟）

**步骤1：** 选中 `tabPageResult`

**步骤2：** 添加控件：

1. **Panel** (Dock: Top, Height: 60)
   - 添加 **Label** (名称: `lblTestStatus`): 
     - Text = "状态: 未执行"
     - Font: Bold, 12pt
     - ForeColor: Gray
   - 添加 **Label** (名称: `lblDuration`):
     - Text = "耗时: -"

2. **TabControl** (名称: `tabResultDetails`, Dock: Fill)
   - 添加TabPage "请求" (tabPageResultRequest):
     - 添加 **RichTextBox** (名称: `rtbRequest`)
       - Dock: Fill
       - ReadOnly: True
       - Font: Consolas, 9pt
   
   - 添加TabPage "响应" (tabPageResultResponse):
     - 添加 **RichTextBox** (名称: `rtbResponse`)
       - Dock: Fill
       - ReadOnly: True
       - Font: Consolas, 9pt
   
   - 添加TabPage "条件评估" (tabPageResultCriteria):
     - 添加 **RichTextBox** (名称: `rtbCriteriaDetail`)
       - Dock: Fill
       - ReadOnly: True
       - Font: Consolas, 9pt

### 11.7 第十天总结检查

**完成情况检查清单：**
- [ ] 主窗体布局完成
- [ ] 请求模板输入区完成
- [ ] 响应模板输入区完成
- [ ] 成功条件输入区完成
- [ ] 变量管理区完成（两个DataGridView）
- [ ] 执行结果区完成
- [ ] 所有控件命名正确
- [ ] 界面美观合理

**第十天产出：**
- 完整的用户界面
- 所有必需的控件
- 清晰的布局结构

---

## 12. 第十一天：界面逻辑实现

### 12.1 初始化代码（30分钟）

**步骤1：** 打开 `Form1.cs` 代码视图

**步骤2：** 添加命名空间引用
```csharp
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
```

EX

Continue

csharp

```
using PLCHttpTester.Core.Models;
using PLCHttpTester.Core.Services;

namespace PLCHttpTester
{
    public partial class MainForm : Form
    {
        private HttpTestService _testService;

        public MainForm()
        {
            InitializeComponent();
            InitializeApplication();
        }

        private void InitializeApplication()
        {
            // 创建服务实例
            _testService = new HttpTestService();

            // 初始化DataGridView
            InitializeDataGridViews();

            // 绑定事件
            BindEvents();

            // 设置默认值
            txtBaseUrl.Text = "http://localhost:8080";
        }

        private void InitializeDataGridViews()
        {
            // 初始化请求变量表格
            dgvRequestVariables.Columns.Clear();
            dgvRequestVariables.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "VariableName",
                HeaderText = "变量名",
                ReadOnly = true,
                Width = 150
            });
            dgvRequestVariables.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "VariableType",
                HeaderText = "类型",
                ReadOnly = true,
                Width = 100
            });
            dgvRequestVariables.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "VariableValue",
                HeaderText = "值",
                ReadOnly = false,
                Width = 200
            });
            dgvRequestVariables.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "FormatString",
                HeaderText = "格式",
                ReadOnly = true,
                Width = 150
            });

            // 初始化响应变量表格
            dgvResponseVariables.Columns.Clear();
            dgvResponseVariables.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "VariableName",
                HeaderText = "变量名",
                ReadOnly = true,
                Width = 150
            });
            dgvResponseVariables.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "VariableType",
                HeaderText = "类型",
                ReadOnly = true,
                Width = 100
            });
            dgvResponseVariables.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "VariableValue",
                HeaderText = "值",
                ReadOnly = true,
                Width = 200
            });
            dgvResponseVariables.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "JsonPath",
                HeaderText = "JSON路径",
                ReadOnly = true,
                Width = 200
            });
        }

        private void BindEvents()
        {
            btnParseTemplates.Click += BtnParseTemplates_Click;
            btnSendRequest.Click += BtnSendRequest_Click;
            dgvRequestVariables.CellValueChanged += DgvRequestVariables_CellValueChanged;
        }
    }
}
```

### 12.2 实现解析模板功能（50分钟）

**步骤1：** 添加解析模板的事件处理

csharp

```
/// <summary>
        /// 解析模板按钮点击事件
        /// </summary>
        private void BtnParseTemplates_Click(object sender, EventArgs e)
        {
            try
            {
                // 清空状态
                lblTestStatus.Text = "状态: 解析中...";
                lblTestStatus.ForeColor = Color.Blue;
                Application.DoEvents();

                // 重置服务
                _testService.Reset();

                // 设置基础URL
                string baseUrl = txtBaseUrl.Text.Trim();
                if (string.IsNullOrWhiteSpace(baseUrl))
                {
                    MessageBox.Show("请输入基础URL", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                _testService.SetBaseUrl(baseUrl);

                // 解析请求模板
                string requestTemplate = txtRequestTemplate.Text;
                if (string.IsNullOrWhiteSpace(requestTemplate))
                {
                    MessageBox.Show("请输入请求模板", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                _testService.LoadRequestTemplate(requestTemplate);

                // 解析响应模板
                string responseTemplate = txtResponseTemplate.Text;
                if (string.IsNullOrWhiteSpace(responseTemplate))
                {
                    MessageBox.Show("请输入响应模板", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                _testService.LoadResponseTemplate(responseTemplate);

                // 解析成功条件
                string criteria = txtSuccessCriteria.Text.Trim();
                if (!string.IsNullOrWhiteSpace(criteria))
                {
                    _testService.LoadSuccessCriteria(criteria);
                }

                // 刷新变量列表
                RefreshVariableGrids();

                // 更新状态
                lblTestStatus.Text = "状态: 解析成功";
                lblTestStatus.ForeColor = Color.Green;

                // 切换到变量管理标签页
                tabResults.SelectedTab = tabPageVariables;

                MessageBox.Show("模板解析成功！请在变量管理中输入变量值", "成功", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                lblTestStatus.Text = "状态: 解析失败";
                lblTestStatus.ForeColor = Color.Red;
                MessageBox.Show($"解析失败:\n{ex.Message}", "错误", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 刷新变量表格
        /// </summary>
        private void RefreshVariableGrids()
        {
            var varManager = _testService.GetVariableManager();

            // 刷新请求变量
            dgvRequestVariables.Rows.Clear();
            var requestVars = varManager.GetRequestVariables();
            foreach (var variable in requestVars)
            {
                int rowIndex = dgvRequestVariables.Rows.Add();
                DataGridViewRow row = dgvRequestVariables.Rows[rowIndex];
                row.Cells["VariableName"].Value = variable.Name;
                row.Cells["VariableType"].Value = variable.Type.ToString();
                row.Cells["VariableValue"].Value = GetDefaultDisplayValue(variable);
                row.Cells["FormatString"].Value = variable.FormatString ?? "";
                row.Tag = variable;  // 保存变量引用
            }

            // 刷新响应变量
            dgvResponseVariables.Rows.Clear();
            var responseVars = varManager.GetResponseVariables();
            var responseTemplate = _testService.GetResponseTemplate();

            foreach (var variable in responseVars)
            {
                int rowIndex = dgvResponseVariables.Rows.Add();
                DataGridViewRow row = dgvResponseVariables.Rows[rowIndex];
                row.Cells["VariableName"].Value = variable.Name;
                row.Cells["VariableType"].Value = variable.Type.ToString();
                row.Cells["VariableValue"].Value = variable.Value?.ToString() ?? "";

                // 查找对应的JSON路径
                if (responseTemplate != null)
                {
                    var mapping = responseTemplate.Mappings.Find(m => m.VariableName == variable.Name);
                    row.Cells["JsonPath"].Value = mapping?.JsonPointer ?? "";
                }

                row.Tag = variable;
            }
        }

        /// <summary>
        /// 获取变量的默认显示值
        /// </summary>
        private string GetDefaultDisplayValue(Variable variable)
        {
            switch (variable.Type)
            {
                case VariableType.DateTime:
                    if (variable.Value is DateTime dt)
                        return variable.FormatString != null ? dt.ToString(variable.FormatString) : dt.ToString();
                    return DateTime.Now.ToString(variable.FormatString ?? "yyyy-MM-dd HH:mm:ss");
                case VariableType.Bool:
                    return "false";
                case VariableType.Int:
                case VariableType.Float:
                    return "0";
                default:
                    return "";
            }
        }
```

### 12.3 实现变量值编辑功能（40分钟）

**步骤1：** 添加变量值变更事件处理

csharp

```
/// <summary>
        /// 请求变量值变更事件
        /// </summary>
        private void DgvRequestVariables_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
                return;

            // 只处理值列的变更
            if (dgvRequestVariables.Columns[e.ColumnIndex].Name != "VariableValue")
                return;

            try
            {
                DataGridViewRow row = dgvRequestVariables.Rows[e.RowIndex];
                Variable variable = row.Tag as Variable;
                if (variable == null)
                    return;

                string newValue = row.Cells["VariableValue"].Value?.ToString();
                if (string.IsNullOrWhiteSpace(newValue))
                    return;

                // 尝试设置变量值
                if (!variable.TrySetValue(newValue))
                {
                    MessageBox.Show($"值 '{newValue}' 无法转换为类型 {variable.Type}", "错误",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    
                    // 恢复默认值
                    row.Cells["VariableValue"].Value = GetDefaultDisplayValue(variable);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"设置变量值时发生错误: {ex.Message}", "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
```

### 12.4 实现发送请求功能（60分钟）

**步骤1：** 添加发送请求的事件处理

csharp

```
/// <summary>
        /// 发送请求按钮点击事件
        /// </summary>
        private async void BtnSendRequest_Click(object sender, EventArgs e)
        {
            try
            {
                // 禁用按钮，防止重复点击
                btnSendRequest.Enabled = false;
                btnParseTemplates.Enabled = false;

                // 更新状态
                lblTestStatus.Text = "状态: 执行中...";
                lblTestStatus.ForeColor = Color.Blue;
                lblDuration.Text = "耗时: -";
                Application.DoEvents();

                // 从DataGridView读取用户输入的变量值
                UpdateVariablesFromGrid();

                // 验证
                var validation = _testService.ValidateBeforeExecution();
                if (!validation.IsValid)
                {
                    MessageBox.Show(validation.GetErrorMessage(), "验证失败",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // 执行测试
                TestResult result = await _testService.ExecuteTestAsync();

                // 显示结果
                DisplayTestResult(result);

                // 刷新响应变量
                RefreshResponseVariables();

                // 切换到结果标签页
                tabResults.SelectedTab = tabPageResult;
            }
            catch (Exception ex)
            {
                lblTestStatus.Text = "状态: 执行失败";
                lblTestStatus.ForeColor = Color.Red;
                MessageBox.Show($"执行测试时发生错误:\n{ex.Message}", "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                // 恢复按钮
                btnSendRequest.Enabled = true;
                btnParseTemplates.Enabled = true;
            }
        }

        /// <summary>
        /// 从表格更新变量值
        /// </summary>
        private void UpdateVariablesFromGrid()
        {
            var varManager = _testService.GetVariableManager();

            foreach (DataGridViewRow row in dgvRequestVariables.Rows)
            {
                Variable variable = row.Tag as Variable;
                if (variable == null)
                    continue;

                string value = row.Cells["VariableValue"].Value?.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    variable.TrySetValue(value);
                }
            }
        }

        /// <summary>
        /// 显示测试结果
        /// </summary>
        private void DisplayTestResult(TestResult result)
        {
            // 更新状态标签
            if (result.Success)
            {
                lblTestStatus.Text = "状态: ✓ 测试成功";
                lblTestStatus.ForeColor = Color.Green;
            }
            else
            {
                lblTestStatus.Text = "状态: ✗ 测试失败";
                lblTestStatus.ForeColor = Color.Red;
            }

            lblDuration.Text = $"耗时: {result.DurationMs} ms";

            // 显示请求
            rtbRequest.Clear();
            rtbRequest.Text = result.RequestText;

            // 显示响应
            rtbResponse.Clear();
            rtbResponse.Text = result.ResponseText;

            // 显示条件评估
            rtbCriteriaDetail.Clear();
            if (!string.IsNullOrWhiteSpace(result.SuccessCriteriaDetail))
            {
                rtbCriteriaDetail.Text = result.SuccessCriteriaDetail;
            }
            else if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
            {
                rtbCriteriaDetail.Text = $"错误信息:\n{result.ErrorMessage}";
            }
            else
            {
                rtbCriteriaDetail.Text = "未设置成功条件";
            }
        }

        /// <summary>
        /// 刷新响应变量显示
        /// </summary>
        private void RefreshResponseVariables()
        {
            var varManager = _testService.GetVariableManager();
            var responseVars = varManager.GetResponseVariables();

            foreach (DataGridViewRow row in dgvResponseVariables.Rows)
            {
                Variable variable = row.Tag as Variable;
                if (variable == null)
                    continue;

                // 查找对应的变量
                var updatedVar = responseVars.Find(v => v.Name == variable.Name);
                if (updatedVar != null)
                {
                    row.Cells["VariableValue"].Value = updatedVar.Value?.ToString() ?? "";
                }
            }
        }
```

### 12.5 添加辅助功能（40分钟）

**步骤1：** 添加菜单栏（可选）

在设计器中：

1. 从工具箱拖拽 `MenuStrip` 到窗体
2. 添加菜单项：
	* 文件
		* 保存模板
		* 加载模板
		* 退出
	* 帮助
		* 使用说明
		* 关于

⠀
**步骤2：** 实现保存和加载模板功能

csharp

```
/// <summary>
        /// 保存模板到文件
        /// </summary>
        private void SaveTemplate()
        {
            try
            {
                using (SaveFileDialog dialog = new SaveFileDialog())
                {
                    dialog.Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*";
                    dialog.DefaultExt = "txt";
                    dialog.FileName = "template.txt";

                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        var content = new System.Text.StringBuilder();
                        content.AppendLine("=== BASE URL ===");
                        content.AppendLine(txtBaseUrl.Text);
                        content.AppendLine();
                        content.AppendLine("=== REQUEST TEMPLATE ===");
                        content.AppendLine(txtRequestTemplate.Text);
                        content.AppendLine();
                        content.AppendLine("=== RESPONSE TEMPLATE ===");
                        content.AppendLine(txtResponseTemplate.Text);
                        content.AppendLine();
                        content.AppendLine("=== SUCCESS CRITERIA ===");
                        content.AppendLine(txtSuccessCriteria.Text);

                        System.IO.File.WriteAllText(dialog.FileName, content.ToString());
                        MessageBox.Show("模板已保存", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 从文件加载模板
        /// </summary>
        private void LoadTemplate()
        {
            try
            {
                using (OpenFileDialog dialog = new OpenFileDialog())
                {
                    dialog.Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*";

                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        string content = System.IO.File.ReadAllText(dialog.FileName);
                        
                        // 简单解析
                        string[] sections = content.Split(new[] { "===" }, StringSplitOptions.None);
                        
                        if (sections.Length >= 8)
                        {
                            txtBaseUrl.Text = sections[2].Trim();
                            txtRequestTemplate.Text = sections[4].Trim();
                            txtResponseTemplate.Text = sections[6].Trim();
                            txtSuccessCriteria.Text = sections[8].Trim();

                            MessageBox.Show("模板已加载", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 显示使用说明
        /// </summary>
        private void ShowHelp()
        {
            string helpText = @"PLC-MES HTTP测试工具 使用说明

1. 填写基础URL
   例如: http://localhost:8080

2. 编写请求模板
   - URL中的变量格式: @(变量名) 或 @(变量名:格式)
   - Header中的变量格式: @(变量名)
   - Body中的变量格式: @类型(变量名) 或 @类型(变量名:格式)
   - 支持的类型: Bool, Int, Float, String, DateTime, Number

3. 编写响应模板
   - 格式与请求类似
   - 会自动提取响应中的值到变量

4. 编写成功条件
   - 支持运算符: = > < >= <= like && ||
   - 例如: $StatusCode=200&&Start_Success

5. 点击"解析模板"按钮

6. 在变量管理中输入变量值

7. 点击"发送请求"执行测试

8. 在执行结果中查看详情";

            MessageBox.Show(helpText, "使用说明", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
```

### 12.6 添加错误处理和用户提示（30分钟）

**步骤1：** 添加全局异常处理

csharp

```
// 在Program.cs的Main方法中添加
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // 添加全局异常处理
            Application.ThreadException += Application_ThreadException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            Application.Run(new MainForm());
        }

        private static void Application_ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            MessageBox.Show($"应用程序错误:\n{e.Exception.Message}", "错误", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception;
            MessageBox.Show($"未处理的异常:\n{ex?.Message}", "严重错误", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
```

### 12.7 第十一天总结检查

**完成情况检查清单：**

* 初始化代码完成
* 解析模板功能实现
* 变量表格刷新功能实现
* 变量值编辑功能实现
* 发送请求功能实现
* 结果显示功能实现
* 保存/加载模板功能实现
* 帮助功能实现
* 错误处理完善
* 所有功能测试通过
**第十一天产出：**

* 完整的界面逻辑
* 所有功能实现
* 用户交互完善
* 可用的完整应用
---

## 13. 第十二天：测试和优化

### 13.1 功能测试清单（60分钟）

**测试1：基本流程测试**

1. 启动程序
2. 检查默认模板是否正确显示
3. 点击"解析模板"
4. 检查变量是否正确提取
5. 输入变量值
6. 点击"发送请求"（需要本地服务器）
7. 检查结果显示

⠀
**测试2：边界条件测试**

测试用例：

csharp

```
// 在Form1.cs中添加测试方法
private void RunTests()
{
    // 测试1：空模板
    TestEmptyTemplate();

    // 测试2：错误格式
    TestInvalidFormat();

    // 测试3：特殊字符
    TestSpecialCharacters();

    // 测试4：大数据量
    TestLargeData();
}

private void TestEmptyTemplate()
{
    try
    {
        _testService.Reset();
        _testService.LoadRequestTemplate("");
        MessageBox.Show("测试失败：应该抛出异常", "测试1");
    }
    catch
    {
        MessageBox.Show("测试通过：正确处理空模板", "测试1");
    }
}

private void TestInvalidFormat()
{
    try
    {
        _testService.Reset();
        _testService.LoadRequestTemplate("这是一个错误的格式");
        MessageBox.Show("测试失败：应该抛出异常", "测试2");
    }
    catch
    {
        MessageBox.Show("测试通过：正确处理错误格式", "测试2");
    }
}

private void TestSpecialCharacters()
{
    // 测试包含特殊字符的JSON
    string template = @"POST /test
Content-Type: application/json

{
    ""text"": @String(SpecialText)
}";
    
    try
    {
        _testService.Reset();
        _testService.LoadRequestTemplate(template);
        var varManager = _testService.GetVariableManager();
        varManager.SetVariableValue("SpecialText", "包含\"引号\"和\n换行");
        
        var requestTemplate = _testService.GetRequestTemplate();
        string request = new HttpRequestProcessor().BuildRequest(
            requestTemplate, 
            varManager.GetAllVariables());
        
        if (request.Contains("\\\"") && request.Contains("\\n"))
        {
            MessageBox.Show("测试通过：正确转义特殊字符", "测试3");
        }
        else
        {
            MessageBox.Show("测试失败：特殊字符转义错误", "测试3");
        }
    }
    catch (Exception ex)
    {
        MessageBox.Show($"测试失败: {ex.Message}", "测试3");
    }
}

private void TestLargeData()
{
    // 测试大量变量
    string template = @"POST /test
Content-Type: application/json

{";
    
    for (int i = 0; i < 100; i++)
    {
        template += $"\n    \"field{i}\": @String(Var{i})";
        if (i < 99) template += ",";
    }
    template += "\n}";
    
    try
    {
        _testService.Reset();
        _testService.LoadRequestTemplate(template);
        var varManager = _testService.GetVariableManager();
        
        if (varManager.Count >= 100)
        {
            MessageBox.Show($"测试通过：成功处理{varManager.Count}个变量", "测试4");
        }
        else
        {
            MessageBox.Show($"测试失败：只处理了{varManager.Count}个变量", "测试4");
        }
    }
    catch (Exception ex)
    {
        MessageBox.Show($"测试失败: {ex.Message}", "测试4");
    }
}
```

### 13.2 性能优化（60分钟）

**优化1：正则表达式优化**

在 `RequestTemplateParser.cs` 中：

csharp

```
// 在类的顶部添加静态字段缓存编译后的正则表达式
private static readonly Regex UrlVariableRegex = new Regex(
    RegexPatterns.UrlVariable, 
    RegexOptions.Compiled);

private static readonly Regex HeaderVariableRegex = new Regex(
    RegexPatterns.HeaderVariable, 
    RegexOptions.Compiled);

private static readonly Regex BodyVariableRegex = new Regex(
    RegexPatterns.BodyVariable, 
    RegexOptions.Compiled);

// 然后在使用时直接使用这些静态实例
private string ProcessUrlVariables(string url, HttpRequestTemplate template)
{
    return UrlVariableRegex.Replace(url, match =>
    {
        // ... 原有代码
    });
}
```

**优化2：StringBuilder使用**

确保所有字符串拼接都使用StringBuilder：

csharp

```
// 在HttpRequestProcessor.cs中
public string BuildRequest(
    HttpRequestTemplate template,
    Dictionary<string, Variable> variables)
{
    var sb = new StringBuilder(1024);  // 预分配容量
    
    // 构建请求...
    
    return sb.ToString();
}
```

**优化3：减少不必要的对象创建**

csharp

```
// 在VariableManager.cs中，复用字典
public Dictionary<string, Variable> GetAllVariables()
{
    // 不要每次都new，直接返回只读视图
    return new Dictionary<string, Variable>(_variables);  // 保持原样，因为需要复制
}
```

### 13.3 用户体验改进（60分钟）

**改进1：添加进度提示**

csharp

```
// 添加一个方法显示进度
private void ShowProgress(string message)
{
    lblTestStatus.Text = $"状态: {message}";
    Application.DoEvents();
}

// 在BtnSendRequest_Click中使用
private async void BtnSendRequest_Click(object sender, EventArgs e)
{
    try
    {
        btnSendRequest.Enabled = false;
        
        ShowProgress("验证变量...");
        UpdateVariablesFromGrid();
        
        ShowProgress("构建请求...");
        // ... 执行测试
        
        ShowProgress("发送请求...");
        TestResult result = await _testService.ExecuteTestAsync();
        
        ShowProgress("处理响应...");
        DisplayTestResult(result);
        
        ShowProgress(result.Success ? "✓ 完成" : "✗ 失败");
    }
    finally
    {
        btnSendRequest.Enabled = true;
    }
}
```

**改进2：添加语法高亮（简单版）**

csharp

```
// 为RichTextBox添加简单的语法高亮
private void HighlightJson(RichTextBox rtb)
{
    rtb.SelectAll();
    rtb.SelectionColor = Color.Black;
    
    // 高亮关键字
    HighlightText(rtb, "true", Color.Blue);
    HighlightText(rtb, "false", Color.Blue);
    HighlightText(rtb, "null", Color.Blue);
    
    // 高亮字符串
    HighlightPattern(rtb, @"""[^""]*""", Color.Brown);
    
    // 高亮数字
    HighlightPattern(rtb, @"\b\d+\.?\d*\b", Color.DarkCyan);
}

private void HighlightText(RichTextBox rtb, string text, Color color)
{
    int start = 0;
    while ((start = rtb.Text.IndexOf(text, start)) != -1)
    {
        rtb.Select(start, text.Length);
        rtb.SelectionColor = color;
        start += text.Length;
    }
}

private void HighlightPattern(RichTextBox rtb, string pattern, Color color)
{
    foreach (Match match in Regex.Matches(rtb.Text, pattern))
    {
        rtb.Select(match.Index, match.Length);
        rtb.SelectionColor = color;
    }
}

// 在显示结果时调用
private void DisplayTestResult(TestResult result)
{
    // ... 原有代码
    
    // 添加语法高亮
    HighlightJson(rtbRequest);
    HighlightJson(rtbResponse);
}
```

**改进3：添加快捷键**

csharp

```
// 在MainForm构造函数中添加
private void InitializeApplication()
{
    // ... 原有代码
    
    // 设置快捷键
    this.KeyPreview = true;
    this.KeyDown += MainForm_KeyDown;
}

private void MainForm_KeyDown(object sender, KeyEventArgs e)
{
    // F5 = 解析模板
    if (e.KeyCode == Keys.F5)
```

EX

Continue

csharp

```
{
        BtnParseTemplates_Click(null, null);
        e.Handled = true;
    }
    // F6 = 发送请求
    else if (e.KeyCode == Keys.F6)
    {
        BtnSendRequest_Click(null, null);
        e.Handled = true;
    }
    // Ctrl+S = 保存模板
    else if (e.Control && e.KeyCode == Keys.S)
    {
        SaveTemplate();
        e.Handled = true;
    }
    // Ctrl+O = 打开模板
    else if (e.Control && e.KeyCode == Keys.O)
    {
        LoadTemplate();
        e.Handled = true;
    }
    // F1 = 帮助
    else if (e.KeyCode == Keys.F1)
    {
        ShowHelp();
        e.Handled = true;
    }
}
```

### 13.4 错误处理完善（40分钟）

**完善1：添加详细的错误日志**

csharp

```
// 在Utils文件夹下创建Logger.cs
using System;
using System.IO;
using System.Text;

namespace PLCHttpTester.Utils
{
    /// <summary>
    /// 简单的日志记录器
    /// </summary>
    public static class Logger
    {
        private static readonly string LogFilePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, 
            "error_log.txt");

        public static void LogError(string message, Exception ex = null)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR");
                sb.AppendLine($"Message: {message}");
                
                if (ex != null)
                {
                    sb.AppendLine($"Exception: {ex.GetType().Name}");
                    sb.AppendLine($"Details: {ex.Message}");
                    sb.AppendLine($"StackTrace: {ex.StackTrace}");
                }
                
                sb.AppendLine(new string('-', 80));
                
                File.AppendAllText(LogFilePath, sb.ToString());
            }
            catch
            {
                // 记录日志失败，忽略
            }
        }

        public static void LogInfo(string message)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] INFO");
                sb.AppendLine($"Message: {message}");
                sb.AppendLine(new string('-', 80));
                
                File.AppendAllText(LogFilePath, sb.ToString());
            }
            catch
            {
                // 忽略
            }
        }
    }
}
```

**完善2：在关键位置添加日志**

csharp

```
// 在MainForm.cs中使用日志
private void BtnParseTemplates_Click(object sender, EventArgs e)
{
    try
    {
        Logger.LogInfo("开始解析模板");
        
        // ... 原有代码
        
        Logger.LogInfo("模板解析成功");
    }
    catch (Exception ex)
    {
        Logger.LogError("模板解析失败", ex);
        // ... 原有错误处理
    }
}

private async void BtnSendRequest_Click(object sender, EventArgs e)
{
    try
    {
        Logger.LogInfo($"开始发送请求到: {txtBaseUrl.Text}");
        
        // ... 原有代码
        
        Logger.LogInfo($"请求完成，状态码: {result.StatusCode}");
    }
    catch (Exception ex)
    {
        Logger.LogError("发送请求失败", ex);
        // ... 原有错误处理
    }
}
```

### 13.5 添加配置文件支持（40分钟）

**步骤1：** 创建配置类

csharp

```
// 在Models文件夹下创建AppConfig.cs
using System;
using System.IO;
using System.Xml.Serialization;

namespace PLCHttpTester.Core.Models
{
    /// <summary>
    /// 应用配置
    /// </summary>
    [Serializable]
    public class AppConfig
    {
        public string LastBaseUrl { get; set; }
        public string LastRequestTemplate { get; set; }
        public string LastResponseTemplate { get; set; }
        public string LastSuccessCriteria { get; set; }

        public AppConfig()
        {
            LastBaseUrl = "http://localhost:8080";
            LastRequestTemplate = "";
            LastResponseTemplate = "";
            LastSuccessCriteria = "";
        }

        /// <summary>
        /// 保存配置
        /// </summary>
        public void Save(string filePath)
        {
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(AppConfig));
                using (StreamWriter writer = new StreamWriter(filePath))
                {
                    serializer.Serialize(writer, this);
                }
            }
            catch
            {
                // 忽略保存失败
            }
        }

        /// <summary>
        /// 加载配置
        /// </summary>
        public static AppConfig Load(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(AppConfig));
                    using (StreamReader reader = new StreamReader(filePath))
                    {
                        return (AppConfig)serializer.Deserialize(reader);
                    }
                }
            }
            catch
            {
                // 加载失败，返回默认配置
            }

            return new AppConfig();
        }
    }
}
```

**步骤2：** 在MainForm中使用配置

csharp

```
// 在MainForm类中添加字段
private AppConfig _config;
private readonly string _configPath = Path.Combine(
    AppDomain.CurrentDomain.BaseDirectory, 
    "config.xml");

// 修改InitializeApplication方法
private void InitializeApplication()
{
    // 加载配置
    _config = AppConfig.Load(_configPath);

    // 创建服务实例
    _testService = new HttpTestService();

    // 初始化DataGridView
    InitializeDataGridViews();

    // 绑定事件
    BindEvents();

    // 恢复上次的内容
    txtBaseUrl.Text = _config.LastBaseUrl;
    txtRequestTemplate.Text = _config.LastRequestTemplate;
    txtResponseTemplate.Text = _config.LastResponseTemplate;
    txtSuccessCriteria.Text = _config.LastSuccessCriteria;
}

// 添加FormClosing事件
private void BindEvents()
{
    btnParseTemplates.Click += BtnParseTemplates_Click;
    btnSendRequest.Click += BtnSendRequest_Click;
    dgvRequestVariables.CellValueChanged += DgvRequestVariables_CellValueChanged;
    this.FormClosing += MainForm_FormClosing;
}

private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
{
    // 保存配置
    _config.LastBaseUrl = txtBaseUrl.Text;
    _config.LastRequestTemplate = txtRequestTemplate.Text;
    _config.LastResponseTemplate = txtResponseTemplate.Text;
    _config.LastSuccessCriteria = txtSuccessCriteria.Text;
    _config.Save(_configPath);

    Logger.LogInfo("应用程序关闭");
}
```

### 13.6 创建用户手册（30分钟）

**步骤1：** 创建README文档

在项目根目录创建 `README.md`：

markdown

```
# PLC-MES HTTP 测试工具

## 功能介绍

这是一个专为PLC与MES系统通信测试设计的工具，支持：
- 自定义HTTP请求模板
- 自动变量提取和管理
- 响应数据自动解析
- 成功条件表达式评估

## 使用步骤

### 1. 设置基础URL
在"请求模板"标签页顶部输入MES服务器地址，例如：
```
http://192.168.1.100:8080
```

### 2. 编写请求模板

#### URL中的变量
格式：`@(变量名)` 或 `@(变量名:格式)`

示例：
```
POST /api/mes/start?stationId=@(StationID:d)&time=@(CurrentTime:yyyyMMdd)
```

#### Header中的变量
格式：`@(变量名)`

示例：
```
User-Agent: BYD_PLC2MES
Authorization: Bearer @(Token)
Content-Type: application/json
```

#### Body中的变量
格式：`@类型(变量名)` 或 `@类型(变量名:格式)`

支持的类型：
- `Bool` - 布尔值（true/false）
- `Int` 或 `Number` - 整数
- `Float` - 浮点数
- `String` - 字符串
- `DateTime` - 日期时间

示例：
```json
{
    "start_time": @String(CurrentTime:yyyy-MM-dd HH:mm:ss.fff),
    "sn": @String(SerialNo),
    "count": @Number(Count),
    "done": @Bool(Done)
}
```

### 3. 编写响应模板

格式与请求模板类似，用于定义如何从响应中提取数据。

示例：
```
200 OK
Content-Type: application/json

{
    "success": @Bool(Start_Success),
    "message": @String(Start_Message),
    "data": {
        "token": @Number(Start_Token)
    }
}
```

### 4. 编写成功条件

支持的运算符：
- 比较：`=` `>` `<` `>=` `<=`
- 字符串匹配：`like`（支持 % 通配符）
- 逻辑：`&&`（与）`||`（或）

特殊变量：
- `$StatusCode` - HTTP状态码

示例：
```
$StatusCode=200 && Start_Success && Start_Token>0
```

### 5. 解析和执行

1. 点击"解析模板"（快捷键：F5）
2. 在"变量管理"标签页输入变量值
3. 点击"发送请求"（快捷键：F6）
4. 在"执行结果"标签页查看结果

## 快捷键

- `F5` - 解析模板
- `F6` - 发送请求
- `Ctrl+S` - 保存模板
- `Ctrl+O` - 打开模板
- `F1` - 帮助

## 常见问题

### 1. 发送请求失败
- 检查基础URL是否正确
- 检查网络连接
- 检查防火墙设置

### 2. 变量提取失败
- 检查响应模板是否与实际响应格式匹配
- 查看日志文件 error_log.txt

### 3. 成功条件不生效
- 检查变量名是否正确
- 检查表达式语法
- 注意变量名区分大小写

## 技术支持

如有问题，请查看：
1. 错误日志：程序目录下的 error_log.txt
2. 配置文件：程序目录下的 config.xml
```

### 13.7 最终测试清单（30分钟）

**完整测试用例：**
```
□ 启动和关闭
  □ 程序正常启动
  □ 加载上次的配置
  □ 正常关闭并保存配置

□ 模板解析
  □ 正确解析URL变量
  □ 正确解析Header变量
  □ 正确解析Body变量
  □ 正确解析响应模板
  □ 正确生成JSON Pointer
  □ 处理解析错误

□ 变量管理
  □ 正确显示请求变量
  □ 正确显示响应变量
  □ 能编辑变量值
  □ 类型转换正确
  □ 处理无效输入

□ HTTP请求
  □ 正确构建请求
  □ 正确替换变量
  □ 正确发送请求
  □ 正确处理超时
  □ 正确处理网络错误

□ 响应处理
  □ 正确解析JSON
  □ 正确提取变量
  □ 正确处理嵌套对象
  □ 正确处理数组
  □ 处理无效JSON

□ 成功条件
  □ 正确解析表达式
  □ 正确求值
  □ 支持所有运算符
  □ 处理语法错误

□ 文件操作
  □ 能保存模板
  □ 能加载模板
  □ 处理文件错误

□ 用户界面
  □ 布局合理
  □ 响应流畅
  □ 错误提示清晰
  □ 快捷键工作正常

□ 性能
  □ 启动速度快
  □ 解析速度快
  □ 界面不卡顿
  □ 内存占用合理
```

### 13.8 打包发布（30分钟）

**步骤1：** 生成Release版本

1. Visual Studio中，切换到 `Release` 模式
2. 右键项目 -> `生成`
3. 在 `bin\Release` 文件夹找到生成的exe文件

**步骤2：** 准备发布文件

创建发布文件夹结构：
```
PLCHttpTester_v1.0/
├── PLCHttpTester.exe
├── README.md
├── 使用说明.txt
└── 示例模板/
    ├── 示例1_启动请求.txt
    └── 示例2_完成请求.txt
```

**步骤3：** 创建示例模板文件

`示例1_启动请求.txt`:
```
=== BASE URL ===
http://localhost:8080

=== REQUEST TEMPLATE ===
POST /api/mes/start?stationId=@(StationID:d)
User-Agent: BYD_PLC2MES
Content-Type: application/json

{
    "start_time": @String(CurrentTime:yyyy-MM-dd HH:mm:ss),
    "sn": @String(SerialNo),
    "station": @Number(StationID)
}

=== RESPONSE TEMPLATE ===
200 OK
Content-Type: application/json

{
    "success": @Bool(Start_Success),
    "message": @String(Start_Message),
    "ticket": @Number(Start_Ticket)
}

=== SUCCESS CRITERIA ===
$StatusCode=200&&Start_Success
```

### 13.9 第十二天总结检查

**完成情况检查清单：**
- [ ] 功能测试全部通过
- [ ] 边界条件测试通过
- [ ] 性能优化完成
- [ ] 用户体验改进完成
- [ ] 错误处理完善
- [ ] 日志功能添加
- [ ] 配置文件功能添加
- [ ] 用户手册编写完成
- [ ] 示例文件准备完成
- [ ] 发布版本生成

**第十二天产出：**
- 经过充分测试的稳定版本
- 完善的错误处理和日志
- 用户手册和示例
- 可发布的应用程序

---

## 14. 项目总结

### 14.1 项目成果

**已完成的功能模块：**

1. **核心解析器**（3个）
   - RequestTemplateParser - 请求模板解析
   - ResponseTemplateParser - 响应模板解析
   - SuccessCriteriaParser - 成功条件解析

2. **处理器**（3个）
   - JsonProcessor - JSON处理
   - HttpRequestProcessor - HTTP请求构建和发送
   - HttpResponseProcessor - HTTP响应处理

3. **服务层**（2个）
   - VariableManager - 变量管理
   - HttpTestService - 主业务逻辑

4. **数据模型**（10+个）
   - Variable, TemplateExpression, HttpRequestTemplate等

5. **工具类**（3个）
   - StringHelper, TypeConverter, Logger

6. **用户界面**
   - 完整的WinForm界面
   - 变量管理
   - 结果展示

### 14.2 代码统计
```
总文件数：约 20 个
总代码行数：约 3000-4000 行
类的数量：约 25 个
方法数量：约 150 个
```

### 14.3 技术要点回顾

1. **正则表达式**：用于模板解析
2. **递归下降解析**：用于表达式解析
3. **访问者模式**：用于JSON树遍历
4. **JSON Pointer**：用于JSON路径定位
5. **异步编程**：用于HTTP请求
6. **事件驱动**：用于UI交互
7. **数据绑定**：用于DataGridView

### 14.4 可能的扩展功能

**未来可以添加的功能：**

1. **批量测试**
   - 支持多个测试用例
   - 测试用例管理
   - 批量执行

2. **测试历史**
   - 保存测试记录
   - 历史查询
   - 结果对比

3. **高级功能**
   - 支持HTTPS
   - 支持证书认证
   - 支持代理设置

4. **脚本支持**
   - 前置脚本
   - 后置脚本
   - 自定义函数

5. **数据驱动测试**
   - 从Excel导入数据
   - 参数化测试
   - 数据生成器

6. **报表功能**
   - 生成测试报告
   - 导出Excel
   - 统计分析

### 14.5 开发经验总结

**成功经验：**
1. 模块化设计，职责清晰
2. 先核心后外围，逐步完善
3. 充分测试，及时发现问题
4. 注重用户体验

**注意事项：**
1. 正则表达式要仔细测试
2. JSON解析要处理各种边界情况
3. 异常处理要全面
4. 用户输入要验证

### 14.6 项目文件清单
```
PLCHttpTester/
├── Core/
│   ├── Models/
│   │   ├── Enums.cs
│   │   ├── Variable.cs
│   │   ├── TemplateExpression.cs
│   │   ├── HttpRequestTemplate.cs
│   │   ├── HttpResponseTemplate.cs
│   │   ├── ResponseMapping.cs
│   │   ├── ConditionNodes.cs
│   │   ├── Token.cs
│   │   ├── HttpResponseData.cs
│   │   ├── TestResult.cs
│   │   └── AppConfig.cs
│   ├── Parsers/
│   │   ├── RequestTemplateParser.cs
│   │   ├── ResponseTemplateParser.cs
│   │   └── SuccessCriteriaParser.cs
│   ├── Processors/
│   │   ├── JsonProcessor.cs
│   │   ├── HttpRequestProcessor.cs
│   │   └── HttpResponseProcessor.cs
│   └── Services/
│       ├── VariableManager.cs
│       └── HttpTestService.cs
├── Utils/
│   ├── StringHelper.cs
│   ├── RegexPatterns.cs
│   ├── TypeConverter.cs
│   └── Logger.cs
├── MainForm.cs
├── MainForm.Designer.cs
├── Program.cs
└── README.md
```

---

## 15. 附录

### 15.1 完整的示例模板

**完整示例1：设备启动**
```
=== BASE URL ===
http://192.168.1.100:8080

=== REQUEST TEMPLATE ===
POST /api/mes/device/start?stationId=@(StationID:d)&timestamp=@(RequestTime:yyyyMMddHHmmss)
User-Agent: BYD_PLC2MES_V1.0
Authorization: Bearer @(AuthToken)
Content-Type: application/json

{
    "station_id": @Number(StationID),
    "start_time": @String(StartTime:yyyy-MM-dd HH:mm:ss.fff),
    "operator": @String(OperatorName),
    "shift": @String(ShiftCode),
    "work_order": @String(WorkOrderNo),
    "product_sn": @String(ProductSN),
    "test_mode": @Bool(IsTestMode)
}

=== RESPONSE TEMPLATE ===
200 OK
Content-Type: application/json

{
    "code": @Number(ResponseCode),
    "success": @Bool(IsSuccess),
    "message": @String(ResponseMessage),
    "data": {
        "process_id": @String(ProcessID),
        "token": @String(ProcessToken),
        "expires_at": @String(TokenExpires),
        "next_station": @Number(NextStationID)
    }
}

=== SUCCESS CRITERIA ===
$StatusCode=200 && IsSuccess && ResponseCode=0 && ProcessToken like "PROC%"
```

### 15.2 常见错误及解决方案

```
错误信息原因解决方案"模板格式错误：缺少请求行"请求模板第一行格式不对确保第一行是 METHOD /path 格式"不支持的数据类型: XXX"使用了不支持的类型只使用 Bool/Int/Float/String/DateTime"JSON解析失败"JSON格式不正确检查JSON语法，特别是引号和逗号"变量未赋值"请求变量没有输入值在变量管理中输入所有必需的值"连接失败"无法连接到服务器检查URL、网络连接、防火墙
```

### 15.3 开发环境要求

**最低要求：**

* Windows 7 或更高
* .NET Framework 4.5
* Visual Studio 2017 或更高
* 2GB RAM
* 100MB 磁盘空间
**推荐配置：**

* Windows 10
* .NET Framework 4.7.2
* Visual Studio 2019/2022
* 4GB RAM
* 500MB 磁盘空间
---

## 恭喜！🎉

如果你完整地跟随这份文档完成了开发，你现在应该有了一个功能完整、可以实际使用的 PLC-MES HTTP 测试工具！

**你学到了：**

1. 如何设计和实现一个完整的桌面应用
2. 字符串解析和正则表达式的实际应用
3. 表达式求值和语法分析
4. JSON处理技巧
5. HTTP通信编程
6. WinForm界面开发
7. 软件测试和优化

⠀
**下一步：**

1. 实际使用这个工具
2. 根据需求添加新功能
3. 优化性能和用户体验
4. 分享给需要的同事

⠀
祝你使用愉快！💪

[Claude](https://claude.ai/chat/230baa53-f735-400a-9728-2476c41a0c93)