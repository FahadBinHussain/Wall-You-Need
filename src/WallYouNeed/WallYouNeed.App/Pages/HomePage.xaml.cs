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
using System.Text.Json;

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
        private ObservableCollection<BackieeImage> Images { get; set; }

        public ObservableCollection<Core.Models.Wallpaper> RecentWallpapers { get; } = new();
        public ObservableCollection<Core.Models.Wallpaper> FavoriteWallpapers { get; } = new();
        public ObservableCollection<Core.Models.Wallpaper> CurrentWallpaper { get; } = new();

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
            Images = new ObservableCollection<BackieeImage>();

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
                    _logger.LogInformation("Found LatestViewAllButton, attaching click handler");
                    
                    // Add a more robust click handler with explicit error handling
                    latestButton.Click += (s, args) => 
                    {
                        try
                        {
                            _logger.LogInformation("LatestViewAllButton clicked");
                            NavigateToCategory("Latest");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Exception in LatestViewAllButton click handler");
                            _snackbarService.Show("Error", $"Navigation error: {ex.Message}", 
                                ControlAppearance.Danger, null, TimeSpan.FromSeconds(3));
                        }
                    };
                }
                else
                {
                    _logger.LogWarning("LatestViewAllButton not found in the XAML");
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
                _snackbarService.Show("Error", $"Failed to setup navigation buttons: {ex.Message}", 
                    ControlAppearance.Danger, null, TimeSpan.FromSeconds(3));
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
            try
            {
                _logger.LogInformation("Featured wallpaper clicked");
                
                // Navigate to the Latest category page to show latest wallpapers
                NavigateToCategory("Latest");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in FeaturedWallpaper_Click handler");
                _snackbarService.Show("Error", $"Navigation error: {ex.Message}", 
                    ControlAppearance.Danger, null, TimeSpan.FromSeconds(3));
            }
        }
        
        private void NavigateToCategory(string categoryName)
        {
            _logger.LogInformation("Navigating to category: {CategoryName}", categoryName);
            
            try
            {
                // For "Latest" category, navigate to BackieeImagesPage instead
                if (categoryName.Equals("Latest", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("Latest category detected - attempting to navigate to BackieeImagesPage");
                    
                    try
                    {
                        // Create an instance of BackieeImagesPage
                        _logger.LogDebug("Resolving BackieeImagesPage from services");
                        var backieeImagesPage = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<BackieeImagesPage>((App.Current as App).Services);
                        _logger.LogDebug("BackieeImagesPage resolved successfully");
                        
                        // Navigate to the BackieeImagesPage using the frame
                        var backieeWindow = Window.GetWindow(this) as MainWindow;
                        if (backieeWindow == null)
                        {
                            _logger.LogError("Failed to get MainWindow from current window");
                            _snackbarService.Show("Error", "Failed to navigate: MainWindow not found", 
                                ControlAppearance.Danger, null, TimeSpan.FromSeconds(2));
                            return;
                        }
                        
                        // Get the ContentFrame directly through its name
                        var backieeFrame = backieeWindow.FindName("ContentFrame") as Frame;
                        if (backieeFrame == null)
                        {
                            _logger.LogError("Failed to get ContentFrame from MainWindow");
                            _snackbarService.Show("Error", "Failed to navigate: ContentFrame not found", 
                                ControlAppearance.Danger, null, TimeSpan.FromSeconds(2));
                            return;
                        }
                        
                        _logger.LogDebug("Navigating to BackieeImagesPage");
                        backieeFrame.Navigate(backieeImagesPage);
                        _logger.LogInformation("Successfully navigated to BackieeImagesPage");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error navigating to BackieeImagesPage");
                        _snackbarService.Show("Error", $"Failed to navigate to BackieeImagesPage: {ex.Message}", 
                            ControlAppearance.Danger, null, TimeSpan.FromSeconds(3));
                    }
                    
                    return;
                }
                
                // For other categories, use CategoryPage as before
                _logger.LogDebug("Using CategoryPage for category: {CategoryName}", categoryName);
                
                // Create an instance of the CategoryPage with all required services
                var categoryPage = new CategoryPage(
                    Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<CategoryPage>>((App.Current as App).Services),
                    _wallpaperService,
                    _snackbarService,
                    _backieeScraperService,
                    _settingsService);
                
                // Set the category
                categoryPage.SetCategory(categoryName);
                
                // Navigate to the CategoryPage using the frame
                var categoryWindow = Window.GetWindow(this) as MainWindow;
                if (categoryWindow != null)
                {
                    // Get the ContentFrame directly through its name
                    var categoryFrame = categoryWindow.FindName("ContentFrame") as Frame;
                    if (categoryFrame != null)
                    {
                        categoryFrame.Navigate(categoryPage);
                        _logger.LogInformation("Successfully navigated to CategoryPage for {CategoryName}", categoryName);
                    }
                    else
                    {
                        _logger.LogError("Failed to get ContentFrame for CategoryPage navigation");
                        _snackbarService.Show("Error", "Failed to navigate: ContentFrame not found", 
                            ControlAppearance.Danger, null, TimeSpan.FromSeconds(2));
                    }
                }
                else
                {
                    _logger.LogError("Failed to get MainWindow for CategoryPage navigation");
                    _snackbarService.Show("Error", "Failed to navigate: MainWindow not found", 
                        ControlAppearance.Danger, null, TimeSpan.FromSeconds(2));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error navigating to category: {CategoryName}", categoryName);
                _snackbarService.Show("Error", $"Failed to navigate to category: {ex.Message}", 
                    ControlAppearance.Danger, null, TimeSpan.FromSeconds(3));
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

        private void ConvertAndAddWallpaper(Wallpaper wallpaper)
        {
            if (wallpaper == null) return;

            var backieeImage = new BackieeImage
            {
                ImageUrl = wallpaper.ThumbnailUrl,
                ImageId = wallpaper.Id,
                IsAiGenerated = wallpaper.Source == WallpaperSource.AI,
                Quality = wallpaper.Metadata.GetValueOrDefault("quality", ""),
                Resolution = $"{wallpaper.Width}x{wallpaper.Height}"
            };

            Images.Add(backieeImage);
        }

        private async Task LoadWallpapersAsync()
        {
            try
            {
                string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backiee_wallpapers.json");
                _logger?.LogInformation($"Loading wallpapers from JSON file: {jsonPath}");

                if (!File.Exists(jsonPath))
                {
                    _logger?.LogError($"JSON file not found: {jsonPath}");
                    return;
                }

                string jsonContent = await File.ReadAllTextAsync(jsonPath);
                var wallpapers = JsonSerializer.Deserialize<List<Wallpaper>>(jsonContent);

                if (wallpapers == null)
                {
                    _logger?.LogError("Failed to deserialize wallpapers from JSON");
                    return;
                }

                foreach (var wallpaper in wallpapers)
                {
                    ConvertAndAddWallpaper(wallpaper);
                }

                _logger?.LogInformation($"Successfully loaded {Images.Count} wallpapers");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading wallpapers from JSON file");
            }
        }
    }
} 