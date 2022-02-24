using System;
using System.IO;

namespace HttpHost.Logger
{
    class Logger
    {
        private string LoggingPath { get; set; }

        enum Severity
        {
            Info,
            Warning,
            Error,
            Critical
        }

        public Logger(string path)
        {
            LoggingPath = path;

            if (!File.Exists(LoggingPath))
            {
                var fs = File.Create(LoggingPath);
                fs.Close();
                fs.Dispose();
            }

        }

        public void LogInformation(string message)
        {
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.Green;
            LogFunc(message, Severity.Info);
        }
        public void LogWarning(string message)
        {
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.Yellow;
            LogFunc(message, Severity.Warning);
        }
        public void LogError(string message)
        {
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.Red;
            LogFunc(message, Severity.Error);
        }
        public void LogCritical(string message)
        {
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.DarkRed;
            LogFunc(message, Severity.Critical);
        }

        private void LogFunc(string message, Severity level)
        {
            string text = $"[ {DateTime.Now.ToString("yyyy-MM-dd | hh:mm:ss")} ] [Level : {level.ToString()}] - {message}\n";

            File.AppendAllText(LoggingPath, string.Format(text, level.ToString(), message));

            Console.Write(String.Format(text, message));
        }
    }
}
