namespace PLC2MES.Utils
{
    public static class RegexPatterns
    {
        public const string UrlVariable = @"@\((\w+)(?::([^)]+))?\)";
        public const string HeaderVariable = @"@\((\w+)(?::([^)]+))?\)";
        public const string BodyVariable = @"@(Bool|Int|Float|String|DateTime|Number)\((\w+)(?::([^)]+))?\)";
        public const string RequestLine = @"^(GET|POST|PUT|DELETE|PATCH)\s+(.+)$";
        public const string StatusLine = @"^(\d{3})\s+(.*)$";
        public const string HeaderLine = @"^([^:]+):\s*(.*)$";
    }
}