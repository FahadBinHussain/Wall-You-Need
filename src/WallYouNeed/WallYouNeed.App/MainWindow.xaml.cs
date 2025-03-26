using Microsoft.Extensions.Logging;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
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
        private System.Windows.Controls.Button _currentActiveButton;

        public MainWindow(
            ILogger<MainWindow> logger,
            IWallpaperService wallpaperService,
            ISettingsService settingsService,
            Wpf.Ui.ISnackbarService snackbarService)
        {
            _logger = logger;
            _wallpaperService = wallpaperService;
            _settingsService = settingsService;

            InitializeComponent();
            
            // Register the SnackbarPresenter with the SnackbarService
            snackbarService.SetSnackbarPresenter(SnackbarPresenter);
            _logger.LogInformation("SnackbarPresenter registered successfully");

            // Set the current active button to Home by default
            _currentActiveButton = HomeButton;

            // Load settings
            LoadSettingsQuietly();
            
            // Navigate to home page by default
            NavigateToPage("Home");
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
            SetActiveButton(HomeButton);
        }

        private void CollectionButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage("Collections");
            SetActiveButton(CollectionButton);
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage("Settings");
            SetActiveButton(SettingsButton);
        }

        private void SetActiveButton(System.Windows.Controls.Button button)
        {
            // Reset previous active button style
            if (_currentActiveButton != null)
            {
                _currentActiveButton.Style = this.FindResource("NavButton") as Style;
                
                // Reset icon and text color
                var stackPanel = _currentActiveButton.Content as StackPanel;
                if (stackPanel != null)
                {
                    var path = stackPanel.Children.OfType<System.Windows.Shapes.Path>().FirstOrDefault();
                    var textBlock = stackPanel.Children.OfType<System.Windows.Controls.TextBlock>().FirstOrDefault();
                    
                    if (path != null)
                    {
                        path.Fill = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#505050"));
                    }
                    
                    if (textBlock != null)
                    {
                        textBlock.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#505050"));
                    }
                }
            }

            // Set new active button
            if (button != null)
            {
                button.Style = this.FindResource("SelectedNavButton") as Style;
                _currentActiveButton = button;
                
                // Set icon and text color to accent color
                var stackPanel = button.Content as StackPanel;
                if (stackPanel != null)
                {
                    var path = stackPanel.Children.OfType<System.Windows.Shapes.Path>().FirstOrDefault();
                    var textBlock = stackPanel.Children.OfType<System.Windows.Controls.TextBlock>().FirstOrDefault();
                    
                    if (path != null)
                    {
                        path.Fill = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0066ff"));
                    }
                    
                    if (textBlock != null)
                    {
                        textBlock.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0066ff"));
                    }
                }
            }
        }

        private void NavigateToPage(string pageName)
        {
            _logger.LogInformation("Navigating to page: {PageName}", pageName);
            
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
                    // Use Frame navigation
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

        private void NavigateToCategoryPage(string categoryName)
        {
            try
            {
                // Get the CategoryPage from service provider
                var app = System.Windows.Application.Current as App;
                var categoryPage = app.Services.GetRequiredService<CategoryPage>();
                
                // Set the category for the page
                categoryPage.SetCategory(categoryName);
                
                // Navigate to the page
                ContentFrame.Navigate(categoryPage);
                _logger.LogInformation("Successfully navigated to category: {CategoryName}", categoryName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error navigating to category: {CategoryName}", categoryName);
                System.Windows.MessageBox.Show($"Error navigating to category: {ex.Message}", 
                    "Navigation Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }
} 