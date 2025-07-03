using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nts = NetTopologySuite.Geometries;

namespace StrategyGame
{
    public class CityGenerationManager
    {
        private readonly Queue<Nts.Polygon> queue = new();
        private readonly PopulationDensityMap density;
        private readonly WaterBodyMap water;
        private readonly TerrainData terrain;
        private bool processing;

        public CityGenerationManager(PopulationDensityMap density,
            WaterBodyMap water,
            TerrainData terrain)
        {
            this.density = density;
            this.water = water;
            this.terrain = terrain;
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
                Nts.Polygon area;
                lock (queue)
                {
                    if (queue.Count == 0)
                    {
                        processing = false;
                        return;
                    }
                    area = queue.Dequeue();
                }

                await RoadNetworkGenerator.GenerateModelAsync(area, 10)
                    .ConfigureAwait(false);
                await Task.Delay(10).ConfigureAwait(false);
            }
        }
    }
}
