using System.Text.RegularExpressions;

namespace PLC2MES.Utils
{
    public static class StringHelper
    {
        private static int _counter = 0;

        public static string GenerateUniqueId()
        {
            return $"VAR_{++_counter}";
        }

        public static void ResetIdCounter()
        {
            _counter = 0; 
        }

        public static string CreatePlaceholder(string id)
        {
            return $"${{{id}}}$";
        }

        public static bool IsPlaceholder(string str)
        {
            if (string.IsNullOrEmpty(str)) return false;
            return Regex.IsMatch(str, "^\\$\\{[A-Za-z0-9_]+\\}\\$$");
        }

        public static string ExtractIdFromPlaceholder(string placeholder)
        {
            var m = Regex.Match(placeholder, "^\\$\\{([A-Za-z0-9_]+)\\}\\$$");
            if (m.Success) return m.Groups[1].Value;
            return null;
        }
    }
}