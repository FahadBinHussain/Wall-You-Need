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
using System.Runtime.InteropServices;

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
            
            // Setup search box behavior
            SetupSearchBox();

            // Load settings
            LoadSettingsQuietly();
            
            // Navigate to home page by default
            NavigateToPage("Home");
        }

        private void SetupWindowControls()
        {
            // We now use default Windows controls, so most of this functionality is removed
            
            // Keep only the search box functionality
            // We no longer need to handle custom window buttons
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

        private void BackieeContentButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage("Backiee Content");
            SetActiveButton(sender as System.Windows.Controls.Button);
        }

        private void SetActiveButton(System.Windows.Controls.Button button)
        {
            if (_currentActiveButton != null)
            {
                // Remove active style from current button
                _currentActiveButton.Style = FindResource("NavButton") as Style;
            }
            
            // Set new active button
            _currentActiveButton = button;
            
            if (_currentActiveButton != null)
            {
                // Apply active style to new button
                _currentActiveButton.Style = FindResource("ActiveNavButton") as Style;
            }
            
            _logger.LogDebug("Active navigation button changed to: {Button}", _currentActiveButton?.Tag?.ToString() ?? "Unknown");
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
                    case "Backiee Content":
                        page = app.Services.GetRequiredService<CategoryPage>();
                        (page as CategoryPage)?.SetCategory("Backiee Content");
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

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            // We've switched to the default window controls, so no need to update button state
        }

        private void SetupSearchBox()
        {
            var searchBox = this.FindName("SearchBox") as System.Windows.Controls.TextBox;
            if (searchBox != null)
            {
                // Set placeholder text behavior
                searchBox.GotFocus += (s, e) => 
                {
                    if (searchBox.Text == "Search...")
                    {
                        searchBox.Text = "";
                        searchBox.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#333333"));
                    }
                };
                
                searchBox.LostFocus += (s, e) => 
                {
                    if (string.IsNullOrWhiteSpace(searchBox.Text))
                    {
                        searchBox.Text = "Search...";
                        searchBox.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#999999"));
                    }
                };
                
                // Handle search submission with Enter key
                searchBox.KeyDown += (s, e) =>
                {
                    if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(searchBox.Text) && searchBox.Text != "Search...")
                    {
                        PerformSearch(searchBox.Text);
                    }
                };
                
                // Setup clear button
                var clearButton = this.FindName("SearchClearButton") as System.Windows.Controls.Button;
                if (clearButton != null)
                {
                    clearButton.Click += (s, e) =>
                    {
                        searchBox.Text = "Search...";
                        searchBox.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#999999"));
                    };
                }
            }
        }
        
        private void PerformSearch(string searchQuery)
        {
            // TODO: Implement search functionality
            _logger.LogInformation("Performing search for: {SearchQuery}", searchQuery);
            // For now, just show a message
            var snackbarService = (System.Windows.Application.Current as App)?.Services.GetService<Wpf.Ui.ISnackbarService>();
            if (snackbarService != null)
            {
                snackbarService.Show("Search", $"Searching for: {searchQuery}", 
                    Wpf.Ui.Controls.ControlAppearance.Info, null, TimeSpan.FromSeconds(2));
            }
        }
    }
} 