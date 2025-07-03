using Nts = NetTopologySuite.Geometries;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace StrategyGame
{
    public static class BuildingGenerator
    {
        public static List<Building> GenerateBuildings(CityDataModel model)
        {
            var bag = new ConcurrentBag<Building>();
            var gf = Nts.GeometryFactory.Default;
            Parallel.ForEach(model.Parcels, parcel =>
            {
                Nts.Geometry foot = parcel.Shape;
                switch (parcel.LandUse)
                {
                    case LandUseType.Commercial:
                        foot = parcel.Shape.Buffer(-parcel.Shape.EnvelopeInternal.Width * 0.05);
                        break;
                    case LandUseType.Residential:
                        foot = parcel.Shape.Buffer(-parcel.Shape.EnvelopeInternal.Width * 0.15);
                        break;
                    case LandUseType.Industrial:
                        var temp = parcel.Shape.Buffer(-parcel.Shape.EnvelopeInternal.Width * 0.1);
                        foot = gf.ToGeometry(temp.EnvelopeInternal);
                        break;
                    case LandUseType.Park:
                        return;
                }

                if (foot is Nts.Polygon p && !foot.IsEmpty)
                    bag.Add(new Building { Footprint = p, LandUse = parcel.LandUse });
            });
            var buildings = bag.ToList();
            model.Buildings = buildings;
            return buildings;
        }
    }
}
