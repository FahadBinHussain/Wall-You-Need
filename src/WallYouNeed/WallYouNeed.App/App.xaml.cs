using System;
using System.Windows;
using System.Windows.Media;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using WallYouNeed.Core.Models;
using WallYouNeed.Core.Services;
using WallYouNeed.Core.Services.Interfaces;
using WallYouNeed.App.Pages;
using WallYouNeed.App.Logging;

namespace WallYouNeed.App
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private ILogger<App> _logger;

        public IServiceProvider Services { get; }

        public App()
        {
            Services = ConfigureServices();
            InitializeComponent();
            
            // Add unhandled exception handlers
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // Create app data paths
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WallYouNeed");
            var logsPath = Path.Combine(appDataPath, "logs");
            
            Directory.CreateDirectory(appDataPath);
            Directory.CreateDirectory(logsPath);
            
            var logFilePath = Path.Combine(logsPath, $"app_{DateTime.Now:yyyyMMdd}.log");
            
            // Create shared logger factory
            var loggerFactory = LoggerFactory.Create(builder => 
                builder.AddDebug()
                    .AddConsole()
                    .AddFile(logFilePath));
            
            // Register services
            services.AddSingleton<ILoggerFactory>(loggerFactory);
            services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
            services.AddSingleton<ILogger<App>>(sp => sp.GetRequiredService<ILoggerFactory>().CreateLogger<App>());
            
            // Register database
            services.AddSingleton<LiteDB.LiteDatabase>(sp => {
                return new LiteDB.LiteDatabase(Path.Combine(appDataPath, "WallYouNeed.db"));
            });
            
            // Register repositories
            services.AddSingleton<WallYouNeed.Core.Repositories.WallpaperRepository>();
            services.AddSingleton<WallYouNeed.Core.Repositories.CollectionRepository>();
            
            // Register core services
            services.AddSingleton<ISnackbarService, SnackbarService>();
            
            // Configure HttpClientFactory with named clients
            services.AddHttpClient("UnsplashApi", client => {
                client.BaseAddress = new Uri("https://api.unsplash.com/");
                // Set default headers for Unsplash API
                client.DefaultRequestHeaders.Add("Accept-Version", "v1");
            });
            
            services.AddHttpClient("PexelsApi", client => {
                client.BaseAddress = new Uri("https://api.pexels.com/v1/");
                // Set default headers for Pexels API
            });
            
            // Register other services
            services.AddSingleton<IWallpaperService, WallpaperService>();
            services.AddSingleton<ICollectionService, CollectionService>();
            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddSingleton<IThemeService, ThemeService>();
            services.AddSingleton<IWallpaperRotationService, WallpaperRotationService>();
            services.AddSingleton<IWallpaperSettingsService, WallpaperSettingsService>();
            services.AddSingleton<WallYouNeed.Core.Utils.WindowsWallpaperUtil>();

            // Register pages
            services.AddTransient<MainWindow>();
            services.AddTransient<HomePage>();
            services.AddTransient<SettingsPage>();
            services.AddTransient<CollectionsPage>();
            services.AddTransient<CategoryPage>();

            return services.BuildServiceProvider();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Create logger first
            _logger = Services.GetRequiredService<ILogger<App>>();
            _logger.LogInformation("Application starting at: {Time}", DateTime.Now);

            try
            {
                // Get necessary services
                var themeService = Services.GetRequiredService<IThemeService>();
                var settingsService = Services.GetRequiredService<ISettingsService>();
                
                // Load settings
                var settings = await settingsService.LoadSettingsAsync();
                themeService.SetTheme(settings.Theme == AppTheme.Dark ? ApplicationTheme.Dark : ApplicationTheme.Light);

                // Show main window
                var mainWindow = Services.GetRequiredService<MainWindow>();
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while starting the application");
                System.Windows.MessageBox.Show(
                    "An error occurred while starting the application. Please check the logs for details.",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                Shutdown();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                _logger?.LogInformation("Application is shutting down...");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "An error occurred while shutting down the application");
            }

            base.OnExit(e);
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                var exception = e.ExceptionObject as Exception;
                _logger?.LogCritical(exception, "Unhandled AppDomain exception: {Message}", exception?.Message);
                
                System.Windows.MessageBox.Show(
                    $"A critical error occurred: {exception?.Message}\nThe application will continue running but may be unstable.",
                    "Critical Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            catch
            {
                // If logging fails, at least show a message box
                System.Windows.MessageBox.Show(
                    "A critical error occurred but details could not be logged.",
                    "Critical Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            try
            {
                _logger?.LogError(e.Exception, "Unhandled UI thread exception: {Message}", e.Exception.Message);
                
                System.Windows.MessageBox.Show(
                    $"An error occurred: {e.Exception.Message}\nThe application will continue running.",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                
                // Mark as handled so the application doesn't crash
                e.Handled = true;
            }
            catch
            {
                // If logging fails, at least show a message box
                System.Windows.MessageBox.Show(
                    "An error occurred but details could not be logged.",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                
                e.Handled = true;
            }
        }

        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            try
            {
                _logger?.LogError(e.Exception, "Unobserved Task exception: {Message}", e.Exception.Message);
                
                // Mark as observed so it doesn't crash the process
                e.SetObserved();
            }
            catch
            {
                // If logging fails, just observe the exception to prevent crashing
                e.SetObserved();
            }
        }
    }
} 