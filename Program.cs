using System;
using System.Windows.Forms;

namespace StrategyGame
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new economy_sim.MainGame());
        }
    }
} 