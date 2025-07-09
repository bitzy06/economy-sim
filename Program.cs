using System;
using System.Threading;
using System.Windows.Forms;

namespace StrategyGame
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            var mainGame = new economy_sim.MainGame();
            var cts = new CancellationTokenSource();

            var uiThread = new Thread(() =>
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(mainGame);
                cts.Cancel();
            });
            uiThread.SetApartmentState(ApartmentState.STA);
            uiThread.Start();

            mainGame.RunGameSimulationLoop(cts.Token).GetAwaiter().GetResult();
            uiThread.Join();
        }
    }} 