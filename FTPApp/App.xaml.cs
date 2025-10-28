namespace FTPApp
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent(); 
            //MainPage = new MainPage(); // Sets your FTP UI as main page

        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    }
}