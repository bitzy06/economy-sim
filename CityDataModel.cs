using System;
using System.Collections.Generic;
using Nts = NetTopologySuite.Geometries;
using NetTopologySuite.Index.Strtree;

namespace StrategyGame
{
    public class CityDataModel
    {
        public Guid Id { get; set; }
        public Nts.Polygon UrbanArea { get; set; }
        public List<LineSegment> RoadNetwork { get; set; } = new();
        public List<Nts.Polygon> RawBlocks { get; set; } = new();
        public List<Parcel> Parcels { get; set; } = new();
        public List<Building> Buildings { get; set; } = new();

        /// <summary>
        /// Parameters controlling how this city's geometry is generated.
        /// </summary>
        public CityGenerationParameters GenerationParameters { get; set; } = new();

        // Spatial index of buildings for faster tile queries
        public STRtree<Building>? BuildingIndex { get; set; }
    }
}
