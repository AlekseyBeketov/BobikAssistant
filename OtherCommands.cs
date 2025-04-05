using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BobikAssistant
{
    public partial class OtherCommands 
    {
        public static Dictionary<string, Action<Label>> Commands = new Dictionary<string, Action<Label>>        {
            {"погод", getWeather },
            {"напоминан", setNotification },
            {"открой браузер", openBrowser },
            {"закрой браузер", closeBrowser }
        };

        private static void getWeather(Label statusLabel)
        {
            statusLabel.Text = "Поиск погоды...";
        }
        private static void setNotification(Label statusLabel)
        {
            statusLabel.Text = "Сейчас поставлю напоминание...";
        }

        private static void openBrowser(Label statusLabel)
        {
            statusLabel.Text = "Открываю браузер...";
        }

        private static void closeBrowser(Label statusLabel)
        {
            statusLabel.Text = "Закрываю браузер...";
        }
    }
}
