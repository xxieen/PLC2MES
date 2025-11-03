namespace PLC2MES.Utils
{
    public static class RegexPatterns
    {
        // unified variable pattern with named groups: optional type, variable name, optional format
        public const string Variable = @"@(?<type>\w+)?\((?<var>\w+)(?::(?<format>[^)]+))?\)";

        public const string RequestLine = @"^(GET|POST|PUT|DELETE|PATCH)\s+(.+)$";
        public const string StatusLine = @"^(\d{3})\s+(.*)$";
        public const string HeaderLine = @"^([^:]+):\s*(.*)$";
    }
}