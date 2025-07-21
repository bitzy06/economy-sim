using System;
using System.IO;
using System.Text.Json;

namespace StrategyGame
{
    /// <summary>
    /// Logs all events published through the message bus to a JSON lines file.
    /// </summary>
    public static class EventLogger
    {
        private static readonly string logFilePath;

        private record LogEntry(DateTime Timestamp, string Type, string Json);

        static EventLogger()
        {
            var logsDir = Path.Combine(Directory.GetCurrentDirectory(), "logs");
            if (!Directory.Exists(logsDir))
            {
                Directory.CreateDirectory(logsDir);
            }

            logFilePath = Path.Combine(logsDir,
                $"event_log_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.jsonl");
        }

        /// <summary>
        /// Serialize and append the provided event to the log file.
        /// </summary>
        public static void Log(object evt)
        {
            try
            {
                var entry = new LogEntry(DateTime.Now,
                    evt.GetType().FullName ?? evt.GetType().Name,
                    JsonSerializer.Serialize(evt, evt.GetType()));
                var json = JsonSerializer.Serialize(entry);
                File.AppendAllText(logFilePath, json + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"EventLogger failed: {ex.Message}");
            }
        }
    }
}
