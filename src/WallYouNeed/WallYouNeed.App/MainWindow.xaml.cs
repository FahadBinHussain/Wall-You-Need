using Microsoft.Extensions.Logging;
using System;
using System.Windows;
using System.Windows.Input;
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

            // Setup window controls
            SetupWindowControls();

            // Load settings
            LoadSettingsQuietly();
            
            // Navigate to home page by default
            NavigateToPage("Home");
        }

        private void SetupWindowControls()
        {
            // Make titlebar draggable
            var titleBar = this.FindName("TitleBar") as System.Windows.Controls.Grid;
            if (titleBar != null)
            {
                titleBar.MouseLeftButtonDown += (s, e) =>
                {
                    if (e.ClickCount == 2)
                    {
                        ToggleMaximize();
                    }
                    else
                    {
                        this.DragMove();
                    }
                };
            }

            // Setup window control buttons
            var minimizeButton = this.FindName("MinimizeButton") as System.Windows.Controls.Button;
            var maximizeButton = this.FindName("MaximizeButton") as System.Windows.Controls.Button;
            var closeButton = this.FindName("CloseButton") as System.Windows.Controls.Button;

            if (minimizeButton != null)
            {
                minimizeButton.Click += (s, e) => this.WindowState = WindowState.Minimized;
            }

            if (maximizeButton != null)
            {
                maximizeButton.Click += (s, e) => ToggleMaximize();
            }

            if (closeButton != null)
            {
                closeButton.Click += (s, e) => this.Close();
            }
        }

        private void ToggleMaximize()
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
            }
            else
            {
                this.WindowState = WindowState.Maximized;
            }
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
                
                // Get the path content from the button
                var path = _currentActiveButton.Content as System.Windows.Shapes.Path;
                if (path != null)
                {
                    path.Fill = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#666666"));
                }
                
                // If button content is a border (for AI button or account)
                var border = _currentActiveButton.Content as System.Windows.Controls.Border;
                if (border != null && border.Child is System.Windows.Controls.TextBlock textBlock)
                {
                    textBlock.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#666666"));
                }
            }

            // Set new active button
            if (button != null)
            {
                button.Style = this.FindResource("ActiveNavButton") as Style;
                _currentActiveButton = button;
                
                // Update the path fill to active color
                var path = button.Content as System.Windows.Shapes.Path;
                if (path != null)
                {
                    path.Fill = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF0000"));
                }
                
                // If button content is a border (for AI button or account)
                var border = button.Content as System.Windows.Controls.Border;
                if (border != null && border.Child is System.Windows.Controls.TextBlock textBlock)
                {
                    textBlock.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF0000"));
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
                System.Windows.Controls.Page page = null;
                
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