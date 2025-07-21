using System;
using System.IO;
using System.Text.Json;

namespace StrategyGame
{
    /// <summary>
    /// Logs all events published on the MessageBus to a structured JSONL file.
    /// </summary>
    public static class EventLogger
    {
        private static readonly object locker = new();
        private static readonly string logsDirectory;
        private static readonly string logFilePath;

        static EventLogger()
        {
            logsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "logs");
            if (!Directory.Exists(logsDirectory))
            {
                Directory.CreateDirectory(logsDirectory);
            }
            logFilePath = Path.Combine(logsDirectory, $"event_log_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.jsonl");
        }

        /// <summary>
        /// Gets the current log file path.
        /// </summary>
        public static string LogFilePath => logFilePath;

        /// <summary>
        /// Record a published event instance to the log file.
        /// </summary>
        public static void Record<T>(T message)
        {
            var envelope = new EventEnvelope
            {
                Timestamp = DateTime.UtcNow,
                Type = typeof(T).AssemblyQualifiedName ?? typeof(T).FullName!,
                Data = JsonSerializer.SerializeToElement(message)
            };

            var json = JsonSerializer.Serialize(envelope);
            lock (locker)
            {
                File.AppendAllText(logFilePath, json + Environment.NewLine);
            }
        }
    }

    /// <summary>
    /// Wrapper used for logging events to disk.
    /// </summary>
    public struct EventEnvelope
    {
        public DateTime Timestamp { get; set; }
        public string Type { get; set; }
        public JsonElement Data { get; set; }
    }
}
