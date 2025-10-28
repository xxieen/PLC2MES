using System;
using System.IO;
using System.Text;

namespace PLC2MES.Utils
{
 public static class Logger
 {
 private static readonly string LogFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plc2mes.log");
 private static readonly object _lock = new object();

 public static void LogInfo(string message)
 {
 Write("INFO", message, null);
 }

 public static void LogError(string message, Exception ex = null)
 {
 Write("ERROR", message, ex);
 }

 private static void Write(string level, string message, Exception ex)
 {
 try
 {
 var sb = new StringBuilder();
 sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {level}");
 sb.AppendLine(message);
 if (ex != null)
 {
 sb.AppendLine("Exception: " + ex.GetType().FullName + ": " + ex.Message);
 sb.AppendLine(ex.StackTrace);
 }
 sb.AppendLine(new string('-',120));

 lock (_lock)
 {
 File.AppendAllText(LogFilePath, sb.ToString());
 }
 }
 catch
 {
 // Swallow exceptions to avoid affecting app flow
 }
 }
 }
}
