using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nts = NetTopologySuite.Geometries;

namespace StrategyGame
{
    public class CityGenerationManager
    {
        private readonly Queue<Nts.Polygon> queue = new();
        private readonly CityGenerationData data;
        private bool processing;

        public CityGenerationManager(CityGenerationData data)
        {
            this.data = data;
            RoadNetworkGenerator.Data = data;
        }

        public void QueueArea(Nts.Polygon area)
        {
            lock (queue)
            {
                queue.Enqueue(area);
                if (!processing)
                {
                    processing = true;
                    _ = ProcessQueue();
                }
            }
        }

        /// <summary>
        /// Returns true if the manager is currently processing queued areas
        /// </summary>
        public bool IsProcessing()
        {
            lock (queue)
            {
                return processing || queue.Count > 0;
            }
        }

        /// <summary>
        /// Gets the current queue count
        /// </summary>
        public int GetQueueCount()
        {
            lock (queue)
            {
                return queue.Count;
            }
        }

        private async Task ProcessQueue()
        {
            while (true)
            {
                List<Nts.Polygon> batch;
                lock (queue)
                {
                    if (queue.Count == 0)
                    {
                        processing = false;
                        return;
                    }
                    batch = new List<Nts.Polygon>(queue);
                    queue.Clear();
                }

                await Parallel.ForEachAsync(batch, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, async (area, token) =>
                {
                    await RoadNetworkGenerator.GenerateModelAsync(area, 10).ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
        }
    }
}
