using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
namespace friction_tester
{
    public static class Logger
    {
        //private static readonly string LogFilePath = "./ApplicationLog.txt";
        //private static readonly string LogFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FrictionTester", "ApplicationLog.txt");
        private static readonly string LogFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ApplicationLog.txt");


        public static void Log(string message)
        {
            try
            {
                var logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - {message}";
                File.AppendAllText(LogFilePath, logEntry + Environment.NewLine);
            }
            catch
            {
                // Avoid logging errors crashing the app
            }
        }

        public static void LogException(Exception ex)
        {
            Log($"ERROR: {ex.Message}\nStack Trace:\n{ex.StackTrace}");
        }
    }
}
