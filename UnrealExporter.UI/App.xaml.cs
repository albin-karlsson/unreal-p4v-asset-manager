﻿using Microsoft.Extensions.DependencyInjection;
using System;
using System.Configuration;
using System.Data;
using System.Windows;
using UnrealExporter.App.Configs;
using UnrealExporter.App.Interfaces;
using UnrealExporter.App.Services;

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
            // Register services as singletons, assuming they are stateless or shared
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<IFileService, FileService>();
            serviceCollection.AddSingleton<IUnrealService, UnrealService>();
            serviceCollection.AddSingleton<IPerforceService, PerforceService>();
            serviceCollection.AddSingleton<IAppConfig, AppConfig>();

            serviceCollection.AddSingleton<MainWindow>();

            ServiceProvider = serviceCollection.BuildServiceProvider();
        }

        private void OnStartup(object sender, StartupEventArgs e)
        {
            var mainWindow = ServiceProvider.GetService<MainWindow>();

            mainWindow.Show();
        }
    }

}

