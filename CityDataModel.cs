using System;
using System.Collections.Generic;

namespace StrategyGame
{
    public class CityDataModel
    {
        public Guid Id { get; set; }
        public List<LineSegment> RoadNetwork { get; set; } = new();
        public List<Parcel> Parcels { get; set; } = new();
        public List<Building> Buildings { get; set; } = new();
    }
}
