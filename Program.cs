using System;
using System.Windows.Forms;

namespace StrategyGame
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            // Ensure shared services are initialized
            _ = GameServices.Bus;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new EconomySim.MainGame());
        }
    }
} 