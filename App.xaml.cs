namespace BobikAssistant
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            MainPage = new AppShell();
        }
        protected override Window CreateWindow(IActivationState activationState)
        {
            var window = base.CreateWindow(activationState);

            // Устанавливаем начальные размеры окна
            window.Width = 650; // ширина
            window.Height = 780; // высота

            // Централизация окна
            window.X = (DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density - window.Width) / 2;
            window.Y = (DeviceDisplay.MainDisplayInfo.Height / DeviceDisplay.MainDisplayInfo.Density - window.Height) / 2;

            return window;
        }
    }
}
