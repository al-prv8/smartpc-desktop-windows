using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SensePC.Desktop.WinUI;
using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.IO;

namespace SensePC.Desktop.WinUI
{
    public partial class App : Application
    {
        public static Window MainWindow { get; private set; }
        public IHost Host { get; }

        public App()
        {
            this.InitializeComponent();
            
            // Setup Serilog
            Log.Logger = new LoggerConfiguration()
                .WriteTo.File(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SensePC", "logs", "native_app.log"), rollingInterval: RollingInterval.Day)
                .CreateLogger();

            // Setup Host and DI
            Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    // Add services here later (Auth, USB, etc)
                })
                .Build();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            MainWindow = new MainWindow();
            MainWindow.Activate();
        }
    }
}
