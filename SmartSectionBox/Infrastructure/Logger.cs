using System;
using System.IO;
using System.Text;

namespace SmartSectionBox.Infrastructure
{
    internal static class Logger
    {
        private static readonly object Gate = new object();
        private static readonly string DirectoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NavisworksSmartSectionBox",
            "Logs");

        public static void Info(string message) => Write("INFO", message, null);
        public static void Warn(string message) => Write("WARN", message, null);
        public static void Error(string message, Exception exception) => Write("ERROR", message, exception);

        private static void Write(string level, string message, Exception exception)
        {
            try
            {
                lock (Gate)
                {
                    Directory.CreateDirectory(DirectoryPath);
                    var file = Path.Combine(DirectoryPath, "smart-section-box-" + DateTime.UtcNow.ToString("yyyy-MM-dd") + ".log");
                    var builder = new StringBuilder();
                    builder.Append(DateTime.UtcNow.ToString("O"));
                    builder.Append(" [");
                    builder.Append(level);
                    builder.Append("] ");
                    builder.AppendLine(message ?? string.Empty);
                    if (exception != null) builder.AppendLine(exception.ToString());
                    File.AppendAllText(file, builder.ToString());
                    TrimOldLogs();
                }
            }
            catch
            {
                // Logging must never destabilize the Navisworks host process.
            }
        }

        private static void TrimOldLogs()
        {
            var files = new DirectoryInfo(DirectoryPath).GetFiles("smart-section-box-*.log");
            Array.Sort(files, (left, right) => right.LastWriteTimeUtc.CompareTo(left.LastWriteTimeUtc));
            for (var i = 14; i < files.Length; i++)
            {
                try { files[i].Delete(); } catch (IOException) { }
            }
        }
    }
}
