using System;
using System.Windows.Forms;

namespace StrategyGame
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            SchemaValidator.Validate();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            WorldSim.Initialize();
            Application.Run(new economy_sim.MainGame());
        }
    }
} 