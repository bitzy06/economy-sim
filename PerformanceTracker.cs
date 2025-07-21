using System;
using System.Collections.Generic;
using System.Linq;

namespace EconomySim
{
    public static class PerformanceTracker
    {
        private class Stat
        {
            public long TotalTicks;
            public int Count;
            public double AverageMs => TotalTicks / (double)Count / TimeSpan.TicksPerMillisecond;
        }

        private static readonly Dictionary<string, Stat> stats = new();
        private static readonly object lockObj = new();

        public static void Record(string area, TimeSpan duration)
        {
            lock (lockObj)
            {
                if (!stats.TryGetValue(area, out var stat))
                {
                    stat = new Stat();
                    stats[area] = stat;
                }
                stat.TotalTicks += duration.Ticks;
                stat.Count++;
            }
        }

        public static IEnumerable<(string Area, int Count, double AvgMs)> GetStats()
        {
            lock (lockObj)
            {
                return stats.Select(kv => (kv.Key, kv.Value.Count, kv.Value.AverageMs)).ToList();
            }
        }

        public static void Clear()
        {
            lock (lockObj)
            {
                stats.Clear();
            }
        }
    }
}
