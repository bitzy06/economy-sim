using System.Threading.Tasks;
using MessageBox.Avalonia;
using MessageBox.Avalonia.DTO;
using MessageBox.Avalonia.Enums;

namespace StrategyGame
{
    public static class DialogHelper
    {
        public static async Task ShowMessage(string message, string title)
        {
            var box = MessageBoxManager.GetMessageBoxStandardWindow(new MessageBoxStandardParams
            {
                ContentTitle = title,
                ContentMessage = message,
                ButtonDefinitions = ButtonEnum.Ok
            });
            await box.Show();
        }
    }
}
