using System;
using System.IO;
using System.Text.Json;
using System.Reflection;

namespace StrategyGame
{
    /// <summary>
    /// Utility to replay logged events by publishing them back onto the MessageBus.
    /// </summary>
    public static class EventReplayer
    {
        public static void Replay(string logFilePath)
        {
            if (!File.Exists(logFilePath))
                throw new FileNotFoundException($"Log file not found: {logFilePath}");

            foreach (var line in File.ReadLines(logFilePath))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var envelope = JsonSerializer.Deserialize<EventEnvelope>(line);
                if (envelope.Type == null)
                    continue;

                var type = Type.GetType(envelope.Type);
                if (type == null)
                    continue;

                object? data = envelope.Data.Deserialize(type, new JsonSerializerOptions());
                if (data == null)
                    continue;

                var method = typeof(MessageBus).GetMethod("Publish")!.MakeGenericMethod(type);
                method.Invoke(MessageBus.Instance, new[] { data });
            }
        }
    }
}
