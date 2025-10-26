# PLC2MES — PLC ↔ MES HTTP 测试工具

本项目为一款基于 C# + Windows Forms 的桌面工具，用于帮助 PLC 工程师编写特定格式的 HTTP 请求模板、发送请求、解析 JSON 响应并提取变量，最终通过用户定义的成功判断表达式判定一次交互是否成功。

特点
-纯 .NET 实现，无需额外 NuGet 包（适合无网络的开发环境）
- 可自定义请求模板与响应模板，自动提取变量
- 支持字符串/数值/布尔/日期类型（String/Int/Float/Bool/DateTime）
- 支持成功条件表达式（&&, ||, =, >, <, >=, <=, like）
- 可在 UI 中编辑请求变量并查看响应与提取到的变量

快速开始

1. 环境要求
 - Windows
 - .NET8 SDK（或在 Visual Studio 中选择目标框架为 net8.0-windows）
 - Visual Studio2022 或更高（推荐）

2. 构建与运行（命令行）
 - 从项目目录（包含 `PLC2MES.csproj`）执行：
 ```bash
 dotnet build
 dotnet run --project PLC2MES/PLC2MES.csproj
 ```
 - 或在 Visual Studio 中打开解决方案并运行。

3. 使用方法（快速演示）
 - 打开程序，填写 `Base URL`（例如 `http://httpbin.org` 用于测试）
 - 在“请求模板”区域粘入模板文本，点击“解析模板”
 - 在“变量管理”中为请求变量填写值
 - 点击“发送请求”并在“执行结果”中查看请求、响应以及成功判断结果

模板语法说明（简要）
- URL / Header 中的变量：`@(VarName)` 或 `@(VarName:format)`（直接替换文本）
- Body 中的变量（必须写类型）：`@Type(VarName)` 或 `@Type(VarName:format)`，Type 支持：`Bool, Int, Float, String, DateTime, Number`

示例：请求模板
```
POST /post?station=@(StationID:d)&mode=@(Mode)
User-Agent: PLC2MES_Tool
Content-Type: application/json

{
 "start_time": @String(CurrentTime:yyyy-MM-dd HH:mm:ss.fff),
 "sn": @String(SerialNo),
 "count": @Number(Count),
 "done": @Bool(Done)
}
```

示例：响应模板
```
200 OK
Content-Type: application/json

{
 "success": @Bool(Start_Success),
 "message": @String(Start_Message),
 "ticket": {
 "token": @Number(Start_Token),
 "expires": @String(Start_TokenExpires)
 },
 "echo": {
 "json": {
 "sn": @String(EchoSerial),
 "start_time": @String(EchoStartTime)
 }
 }
}
```

示例：成功条件
```
$StatusCode=200 && Start_Success && Start_Token>0 && EchoSerial like "SN%"
```

项目结构（概要）
```
PLC2MES/
├── Core/
│ ├── Models/ // 数据模型：Variable, TemplateExpression, HttpRequestTemplate, HttpResponseTemplate ...
│ ├── Parsers/ //解析器：RequestTemplateParser, ResponseTemplateParser, SuccessCriteriaParser
│ ├── Processors/ //处理器：JsonProcessor, HttpRequestProcessor, HttpResponseProcessor
│ └── Services/ //业务服务：VariableManager, HttpTestService
├── Utils/ // 辅助工具：StringHelper, TypeConverter, RegexPatterns, Logger
├── Form1.cs // WinForms UI逻辑
├── Form1.Designer.cs // UI 控件
├── Program.cs
└── PLC2MES.csproj
```

离线开发注意事项
- 本项目不依赖外部 NuGet 包，所有依赖均使用 .NET 标准库（System.Text.Json、System.Net 等），适合离线机器开发。
- 若在你本地环境使用旧版本的 .NET，请确保 JSON 库可用或替换为 `JavaScriptSerializer`（需引用 `System.Web.Extensions`），但当前仓库使用 `System.Text.Json`。

贡献指南
- Fork 后在 feature 分支上开发，提交 PR 前确保：
 -代码遵循现有命名与风格
 -运行 `dotnet build` 无错误
 - 如改动解析逻辑，需添加或更新示例模板以便测试

许可证
- 本项目默认使用 MIT许可证（如需变更请修改 `LICENSE`）。

联系方式
- 开发者：内部使用，按公司流程管理版本与发布。
