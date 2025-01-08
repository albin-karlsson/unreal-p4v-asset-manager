using Microsoft.Extensions.DependencyInjection;
using System.Configuration;
using System.Data;
using System.Windows;
using UnrealExporter.App;
using UnrealExporter.App.Interfaces;

namespace UnrealExporter.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static IServiceProvider ServiceProvider { get; private set; }

        public App()
        {
            var serviceCollection = new ServiceCollection();

            serviceCollection.AddScoped<IFileService, FileManager>();
            serviceCollection.AddScoped<IPerforceService, PerforceManager>();
            serviceCollection.AddScoped<IUnrealService, UnrealManager>();

            ServiceProvider = serviceCollection.BuildServiceProvider();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();

            mainWindow.Show();
        }
    }

}
