using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Wpf.Ui;
using Wpf.Ui.Controls;
using WallYouNeed.Core.Models;
using WallYouNeed.Core.Services;
using WallYouNeed.Core.Services.Interfaces;
using WallYouNeed.App.Pages;
using System.Collections.Generic;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;

namespace WallYouNeed.App.Pages
{
    public partial class HomePage : Page, INavigableView<HomePage>
    {
        private readonly ILogger<HomePage> _logger;
        private readonly ISnackbarService _snackbarService;
        private readonly IWallpaperService _wallpaperService;
        private readonly ICollectionService _collectionService;
        private readonly ISettingsService _settingsService;

        public ObservableCollection<Wallpaper> RecentWallpapers { get; } = new();
        public ObservableCollection<Wallpaper> FavoriteWallpapers { get; } = new();
        public ObservableCollection<Wallpaper> CurrentWallpaper { get; } = new();

        public HomePage ViewModel => this;

        public HomePage(
            ILogger<HomePage> logger,
            ISnackbarService snackbarService,
            IWallpaperService wallpaperService,
            ICollectionService collectionService,
            ISettingsService settingsService)
        {
            _logger = logger;
            _snackbarService = snackbarService;
            _wallpaperService = wallpaperService;
            _collectionService = collectionService;
            _settingsService = settingsService;

            InitializeComponent();
            DataContext = this;

            Loaded += HomePage_Loaded;
            
            // Set up event handlers for "View all" buttons after the page is loaded
            Loaded += SetupViewAllButtons;
        }
        
        private void SetupViewAllButtons(object sender, RoutedEventArgs e)
        {
            try 
            {
                // Connect button click handlers if the elements exist
                if (FindName("LatestViewAllButton") is System.Windows.Controls.Button latestButton)
                {
                    latestButton.Click += (s, e) => NavigateToCategory("Latest");
                }
                
                if (FindName("WeeklyViewAllButton") is System.Windows.Controls.Button weeklyButton)
                {
                    weeklyButton.Click += (s, e) => NavigateToCategory("Weekly");
                }
                
                if (FindName("MonthlyViewAllButton") is System.Windows.Controls.Button monthlyButton)
                {
                    monthlyButton.Click += (s, e) => NavigateToCategory("Monthly");
                }
                
                // Connect category card click handlers
                if (FindName("NatureCategoryCard") is Border natureCard)
                {
                    natureCard.MouseLeftButtonUp += (s, e) => NavigateToCategory("Nature");
                }
                
                if (FindName("ArchitectureCategoryCard") is Border architectureCard)
                {
                    architectureCard.MouseLeftButtonUp += (s, e) => NavigateToCategory("Architecture");
                }
                
                if (FindName("AbstractCategoryCard") is Border abstractCard)
                {
                    abstractCard.MouseLeftButtonUp += (s, e) => NavigateToCategory("Abstract");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting up view all buttons");
            }
        }
        
        private void NavigateToCategory(string categoryName)
        {
            try
            {
                _logger.LogInformation("Navigating to category: {CategoryName}", categoryName);
                
                // Get the app instance
                var app = System.Windows.Application.Current as App;
                if (app == null)
                {
                    _logger.LogError("Failed to get App instance");
                    return;
                }
                
                // Get the CategoryPage from service provider and navigate to it
                var categoryPage = app.Services.GetRequiredService<CategoryPage>();
                categoryPage.SetCategory(categoryName);
                
                // Get the main window's ContentFrame and navigate to the category page
                if (Window.GetWindow(this) is MainWindow mainWindow)
                {
                    if (mainWindow.FindName("ContentFrame") is Frame contentFrame)
                    {
                        contentFrame.Navigate(categoryPage);
                        _logger.LogInformation("Successfully navigated to category: {CategoryName}", categoryName);
                    }
                }
                else
                {
                    _logger.LogError("Failed to get MainWindow instance");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error navigating to category: {CategoryName}", categoryName);
                _snackbarService.Show("Error", $"Failed to navigate to {categoryName} category", 
                    ControlAppearance.Danger, null, TimeSpan.FromSeconds(2));
            }
        }
        
        private async void HomePage_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await LoadFeaturedWallpapersAsync();
                await LoadDailyPopularWallpapersAsync();
                
                // Placeholder images can be set in XAML for faster initial load
                // LoadPlaceholderImages();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load home page");
                _snackbarService.Show("Error", "Failed to load home page", Wpf.Ui.Controls.ControlAppearance.Danger, null, TimeSpan.FromSeconds(2));
            }
        }

        private async Task LoadFeaturedWallpapersAsync()
        {
            try
            {
                // Since GetFeaturedWallpapersAsync is not available in IWallpaperService,
                // we'll use the existing methods to get wallpapers for featured display
                var recentWallpapers = await _wallpaperService.GetRecentWallpapersAsync(3);
                
                if (recentWallpapers != null && recentWallpapers.Any())
                {
                    var wallpapersList = recentWallpapers.ToList();
                    
                    // Try to load the first wallpaper as the main featured image
                    if (wallpapersList.Count > 0)
                    {
                        await LoadImageFromWallpaper(wallpapersList[0], FeaturedWallpaperImage);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load featured wallpapers");
            }
        }
        
        private async Task LoadDailyPopularWallpapersAsync()
        {
            try
            {
                // In a real app, this would get daily popular wallpapers from a server
                // For now, we'll use recent wallpapers as a placeholder
                var popularWallpapers = await _wallpaperService.GetRecentWallpapersAsync(6);
                
                // With real implementation, you would map these to the grid cards
                // Here we're just providing the scaffolding for future implementation
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load daily popular wallpapers");
            }
        }
        
        private async Task LoadImageFromWallpaper(Wallpaper wallpaper, System.Windows.Controls.Image imageControl)
        {
            if (wallpaper == null || imageControl == null)
            {
                return;
            }
            
            if (!string.IsNullOrEmpty(wallpaper.FilePath) && File.Exists(wallpaper.FilePath))
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(wallpaper.FilePath);
                    bitmap.EndInit();
                    
                    imageControl.Source = bitmap;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading wallpaper image: {FilePath}", wallpaper.FilePath);
                }
            }
        }
        
        private void LoadPlaceholderImages()
        {
            // This would be used for design or if real images are unavailable
            // In a real implementation, you'd replace these with dynamic content
            
            // Example of loading a placeholder from resources if available
            // ResourceDictionary resourceDictionary = new ResourceDictionary
            // {
            //     Source = new Uri("/WallYouNeed.App;component/Resources/Images.xaml", UriKind.Relative)
            // };
            // 
            // if (resourceDictionary.Contains("PlaceholderImage"))
            // {
            //     FeaturedWallpaperImage.Source = resourceDictionary["PlaceholderImage"] as ImageSource;
            // }
        }
        
        private async Task ApplySelectedWallpaper(string wallpaperId)
        {
            if (string.IsNullOrEmpty(wallpaperId))
            {
                return;
            }
            
            try
            {
                bool success = await _wallpaperService.ApplyWallpaperAsync(wallpaperId);
                
                if (success)
                {
                    _snackbarService.Show("Success", "Wallpaper applied successfully",
                        ControlAppearance.Success, null, TimeSpan.FromSeconds(2));
                }
                else
                {
                    _snackbarService.Show("Error", "Failed to apply wallpaper",
                        ControlAppearance.Danger, null, TimeSpan.FromSeconds(2));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying wallpaper {WallpaperId}", wallpaperId);
                _snackbarService.Show("Error", $"Error applying wallpaper: {ex.Message}",
                    ControlAppearance.Danger, null, TimeSpan.FromSeconds(2));
            }
        }
        
        // Future methods to handle AI wallpaper generation, downloading, etc.
        
        // In the future, you'd implement event handlers for all the new UI elements
        // Such as download buttons, like buttons, navigation to specific categories, etc.
    }
} 