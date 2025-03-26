using Microsoft.Extensions.Logging;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using WallYouNeed.Core.Services.Interfaces;
using WallYouNeed.Core.Models;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using WallYouNeed.App.Pages;

namespace WallYouNeed.App
{
    public partial class MainWindow : Window
    {
        private readonly ILogger<MainWindow> _logger;
        private readonly IWallpaperService _wallpaperService;
        private readonly ISettingsService _settingsService;

        public MainWindow(
            ILogger<MainWindow> logger,
            IWallpaperService wallpaperService,
            ISettingsService settingsService)
        {
            _logger = logger;
            _wallpaperService = wallpaperService;
            _settingsService = settingsService;

            InitializeComponent();

            // Initialize UI
            HomeButton.Click += HomeButton_Click;
            CollectionButton.Click += CollectionButton_Click;
            SettingsButton.Click += SettingsButton_Click;
            // ApplyRandomWallpaperButton was moved to the Home page and will be initialized there

            // Navigate to home page by default
            NavigateToPage("Home");

            // Load settings
            LoadSettingsQuietly();
        }

        private async void LoadSettingsQuietly()
        {
            try
            {
                var settings = await _settingsService.LoadSettingsAsync();
                _logger.LogInformation("Settings loaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load settings");
            }
        }

        private void HomeButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage("Home");
        }

        private void CollectionButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage("Collections");
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage("Settings");
        }

        private async void ApplyRandomWallpaper_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _logger.LogInformation("Applying random wallpaper");
                
                // Get all wallpapers and pick a random one
                var wallpapers = await _wallpaperService.GetAllWallpapersAsync();
                var wallpapersList = wallpapers.ToList();
                
                if (wallpapersList.Count == 0)
                {
                    _logger.LogWarning("No wallpaper found to apply");
                    System.Windows.MessageBox.Show("No wallpaper found to apply. Add some wallpapers first!", 
                        "No Wallpaper", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    return;
                }
                
                // Select a random wallpaper
                var random = new Random();
                var wallpaper = wallpapersList[random.Next(wallpapersList.Count)];

                bool success = await _wallpaperService.ApplyWallpaperAsync(wallpaper.Id);
                if (success)
                {
                    _logger.LogInformation("Applied random wallpaper: {WallpaperId}", wallpaper.Id);
                    System.Windows.MessageBox.Show($"Wallpaper applied successfully!", 
                        "Success", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                else
                {
                    _logger.LogWarning("Failed to apply wallpaper: {WallpaperId}", wallpaper.Id);
                    System.Windows.MessageBox.Show("Failed to apply wallpaper. Please try again.", 
                        "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying random wallpaper");
                System.Windows.MessageBox.Show($"Error applying wallpaper: {ex.Message}", 
                    "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void NavigateToPage(string pageName)
        {
            _logger.LogInformation("Navigating to page: {PageName}", pageName);
            
            // Reset button appearances
            ResetButtonStyles();
            
            // Set active button
            switch (pageName)
            {
                case "Home":
                    HomeButton.FontWeight = FontWeights.Bold;
                    break;
                case "Collections":
                    CollectionButton.FontWeight = FontWeights.Bold;
                    break;
                case "Settings":
                    SettingsButton.FontWeight = FontWeights.Bold;
                    break;
                default:
                    _logger.LogWarning("Unknown page: {PageName}", pageName);
                    return;
            }

            try
            {
                // Get the right page from service provider
                var app = System.Windows.Application.Current as App;
                Page page = null;
                
                switch (pageName)
                {
                    case "Home":
                        page = app.Services.GetRequiredService<HomePage>();
                        break;
                    case "Collections":
                        page = app.Services.GetRequiredService<CollectionsPage>();
                        break;
                    case "Settings":
                        page = app.Services.GetRequiredService<SettingsPage>();
                        break;
                }
                
                if (page != null)
                {
                    // Use Frame navigation instead of direct content assignment
                    ContentFrame.Navigate(page);
                    _logger.LogInformation("Successfully navigated to {PageName}", pageName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error navigating to page: {PageName}", pageName);
                System.Windows.MessageBox.Show($"Error navigating to page: {ex.Message}", 
                    "Navigation Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void ResetButtonStyles()
        {
            HomeButton.FontWeight = FontWeights.Normal;
            CollectionButton.FontWeight = FontWeights.Normal;
            SettingsButton.FontWeight = FontWeights.Normal;
        }
    }
} 