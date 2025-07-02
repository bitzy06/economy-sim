using NetTopologySuite.Geometries;
using System.Collections.Generic;

namespace StrategyGame
{
    public static class BuildingGenerator
    {
        public static List<Building> GenerateBuildings(CityDataModel model)
        {
            var buildings = new List<Building>();
            var gf = GeometryFactory.Default;
            foreach (var parcel in model.Parcels)
            {
                Geometry foot = parcel.Shape;
                switch (parcel.LandUse)
                {
                    case LandUseType.Residential:
                        foot = parcel.Shape.Buffer(-parcel.Shape.EnvelopeInternal.Width * 0.1);
                        break;
                    case LandUseType.Commercial:
                        foot = parcel.Shape.Buffer(-parcel.Shape.EnvelopeInternal.Width * 0.02);
                        break;
                    case LandUseType.Industrial:
                        foot = parcel.Shape.Buffer(-parcel.Shape.EnvelopeInternal.Width * 0.05);
                        break;
                    case LandUseType.Park:
                        continue;
                }
                if (foot is Polygon p && !foot.IsEmpty)
                    buildings.Add(new Building { Footprint = p, LandUse = parcel.LandUse });
            }
            model.Buildings = buildings;
            return buildings;
        }
    }
}
