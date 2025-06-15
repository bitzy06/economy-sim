using System.Drawing;

namespace StrategyGame
{
    public class Plane
    {
        public Point Position { get; set; }
        public float Speed { get; set; }
        public Point Direction { get; set; } // A vector indicating direction of travel
        public float Altitude { get; set; }
        // Add other relevant properties like Type, Airline, FlightNumber, etc. later if needed
    }
}
