namespace PLC2MES.Utils
{
    public static class RegexPatterns
    {
        // unified variable pattern with named groups: optional type (supports Array<T> or T[]), variable name, optional format
        public const string Variable = @"@(?<type>\w+(?:<\w+>|\[\])?)?\((?<var>\w+)(?::(?<format>[^)]+))?\)";

        public const string RequestLine = @"^(GET|POST|PUT|DELETE|PATCH)\s+(.+)$";
        public const string StatusLine = @"^(\d{3})\s+(.*)$";
        public const string HeaderLine = @"^([^:]+):\s*(.*)$";
    }
}