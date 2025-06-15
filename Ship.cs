using System.Drawing;

namespace StrategyGame
{
    public class Ship
    {
        public Point Position { get; set; }
        public float Speed { get; set; }
        public Point Direction { get; set; } // A vector indicating direction of travel
        public string Type { get; set; } // E.g., Cargo, Tanker, Naval
                                         // Add other relevant properties like Name, Flag, etc. later if needed
    }
}
