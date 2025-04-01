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
using WallYouNeed.Core.Repositories;
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
        private readonly IBackieeScraperService _backieeScraperService;

        public ObservableCollection<Wallpaper> RecentWallpapers { get; } = new();
        public ObservableCollection<Wallpaper> FavoriteWallpapers { get; } = new();
        public ObservableCollection<Wallpaper> CurrentWallpaper { get; } = new();

        public HomePage ViewModel => this;

        public HomePage(
            ILogger<HomePage> logger,
            ISnackbarService snackbarService,
            IWallpaperService wallpaperService,
            ICollectionService collectionService,
            ISettingsService settingsService,
            IBackieeScraperService backieeScraperService)
        {
            _logger = logger;
            _snackbarService = snackbarService;
            _wallpaperService = wallpaperService;
            _collectionService = collectionService;
            _settingsService = settingsService;
            _backieeScraperService = backieeScraperService;

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
        
        private async Task LoadImageFromUrl(string imageUrl, System.Windows.Controls.Image imageControl)
        {
            if (string.IsNullOrEmpty(imageUrl) || imageControl == null)
            {
                return;
            }
            
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(imageUrl);
                bitmap.EndInit();
                
                imageControl.Source = bitmap;
                _logger.LogInformation("Successfully loaded image from URL: {ImageUrl}", imageUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading image from URL: {ImageUrl}", imageUrl);
            }
        }
        
        private void LoadPlaceholderImage(System.Windows.Controls.Image imageControl, Wallpaper wallpaper)
        {
            try
            {
                _logger.LogDebug("Loading placeholder image for wallpaper ID: {WallpaperId}", wallpaper.Id);
                
                // First try the thumbnail URL
                if (!string.IsNullOrEmpty(wallpaper.ThumbnailUrl))
                {
                    try
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.UriSource = new Uri(wallpaper.ThumbnailUrl);
                        bitmap.EndInit();
                        
                        imageControl.Source = bitmap;
                        _logger.LogInformation("Successfully loaded placeholder from thumbnail URL: {Url}", wallpaper.ThumbnailUrl);
                        return;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error loading thumbnail URL in placeholder: {Url}", wallpaper.ThumbnailUrl);
                    }
                }
                
                // If thumbnail fails, try source URL
                if (!string.IsNullOrEmpty(wallpaper.SourceUrl))
                {
                    try
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.UriSource = new Uri(wallpaper.SourceUrl);
                        bitmap.EndInit();
                        
                        imageControl.Source = bitmap;
                        _logger.LogInformation("Successfully loaded placeholder from source URL: {Url}", wallpaper.SourceUrl);
                        return;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error loading source URL in placeholder: {Url}", wallpaper.SourceUrl);
                    }
                }
                
                // If all loading attempts failed, show a simple "No Image" text
                _logger.LogWarning("All image loading attempts failed, showing 'No Image Available' text");
                
                // Create a very simple "No Image Available" placeholder
                var drawingVisual = new System.Windows.Media.DrawingVisual();
                using (var drawingContext = drawingVisual.RenderOpen())
                {
                    // Draw background
                    drawingContext.DrawRectangle(
                        new System.Windows.Media.SolidColorBrush(
                            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#607D8B")),
                        null,
                        new System.Windows.Rect(0, 0, 280, 160));
                    
                    // Draw "No Image Available" text
                    var text = new System.Windows.Media.FormattedText(
                        "No Image Available",
                        System.Globalization.CultureInfo.CurrentCulture,
                        System.Windows.FlowDirection.LeftToRight,
                        new System.Windows.Media.Typeface("Segoe UI"),
                        16,
                        System.Windows.Media.Brushes.White,
                        System.Windows.Media.VisualTreeHelper.GetDpi(drawingVisual).PixelsPerDip);
                    
                    // Center the text
                    drawingContext.DrawText(text, new System.Windows.Point(
                        (280 - text.Width) / 2,
                        (160 - text.Height) / 2));
                }
                
                // Convert drawing to bitmap
                var renderTarget = new System.Windows.Media.Imaging.RenderTargetBitmap(
                    280, 160, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                renderTarget.Render(drawingVisual);
                
                // Set as image source
                imageControl.Source = renderTarget;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load placeholder image: {Error}", ex.Message);
            }
        }
        
        private void FeaturedWallpaper_Click(object sender, RoutedEventArgs e)
        {
            _logger.LogInformation("Featured wallpaper clicked");
            
            // Navigate to the featured wallpaper detail page
            // For now, just navigate to latest category
            NavigateToCategory("Latest");
        }
        
        private void NavigateToCategory(string categoryName)
        {
            _logger.LogInformation("Navigating to category: {CategoryName}", categoryName);
            
            try
            {
                // Create an instance of the CategoryPage
                var categoryPage = new CategoryPage(
                    Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<CategoryPage>>((App.Current as App).Services),
                    _wallpaperService,
                    _snackbarService,
                    _backieeScraperService);
                
                // Set the category
                categoryPage.SetCategory(categoryName);
                
                // Navigate to the CategoryPage using the frame
                if (Window.GetWindow(this) is MainWindow mainWindow &&
                    mainWindow.FindName("MainFrame") is Frame mainFrame)
                {
                    mainFrame.Navigate(categoryPage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error navigating to category: {CategoryName}", categoryName);
                _snackbarService.Show("Error", "Failed to navigate to category", 
                    ControlAppearance.Danger, null, TimeSpan.FromSeconds(2));
            }
        }
        
        private async void HomePage_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await LoadInitialContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading initial content");
                _snackbarService.Show("Error", "Failed to load initial content", 
                    ControlAppearance.Danger, null, TimeSpan.FromSeconds(2));
            }
        }

        private async Task LoadInitialContent()
        {
            try
            {
                _logger.LogInformation("Loading initial content");
                
                // Show loading indicator
                if (FindName("ContentLoadingRing") is Wpf.Ui.Controls.ProgressRing progressRing)
                {
                    progressRing.Visibility = Visibility.Visible;
                }
                
                // Load recent wallpapers
                var recentWallpapers = await _wallpaperService.GetRecentWallpapersAsync(6);
                RecentWallpapers.Clear();
                
                foreach (var wallpaper in recentWallpapers)
                {
                    RecentWallpapers.Add(wallpaper);
                }
                
                _logger.LogInformation("Loaded {Count} recent wallpapers", RecentWallpapers.Count);
                
                // Load favorite wallpapers
                var favorites = await _wallpaperService.GetFavoriteWallpapersAsync();
                FavoriteWallpapers.Clear();
                
                foreach (var favorite in favorites)
                {
                    FavoriteWallpapers.Add(favorite);
                }
                
                _logger.LogInformation("Loaded {Count} favorite wallpapers", FavoriteWallpapers.Count);
                
                // Hide loading indicator
                if (FindName("ContentLoadingRing") is Wpf.Ui.Controls.ProgressRing loadingRing)
                {
                    loadingRing.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading initial content");
                throw;
            }
        }
    }
} 