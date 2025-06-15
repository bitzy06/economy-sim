using System.Drawing;

namespace StrategyGame
{
    public class Thunderstorm
    {
        public Point Position { get; set; }
        public Size Size { get; set; } // Represents the area covered by the storm
        public float Intensity { get; set; } // E.g., rainfall rate, wind speed
                                             // Add other relevant properties like Duration, MovementDirection, etc. later if needed
    }
}
