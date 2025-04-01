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
                
                // Add a button to load the latest Backiee wallpapers
                if (FindName("LoadLatestBackieeButton") is System.Windows.Controls.Button loadLatestButton)
                {
                    loadLatestButton.Click += LoadLatestBackieeButton_Click;
                }
                else
                {
                    // If the button doesn't exist in XAML, try to add it programmatically
                    try
                    {
                        var mainPanel = FindName("MainPanel") as System.Windows.Controls.Panel;
                        if (mainPanel != null)
                        {
                            var button = new Wpf.Ui.Controls.Button
                            {
                                Content = "Load Latest Wallpapers",
                                Margin = new Thickness(0, 10, 0, 10),
                                HorizontalAlignment = System.Windows.HorizontalAlignment.Center
                            };
                            button.Click += LoadLatestBackieeButton_Click;
                            mainPanel.Children.Add(button);
                            _logger.LogInformation("Added Load Latest Wallpapers button programmatically");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to add Load Latest Wallpapers button programmatically");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting up view all buttons");
            }
        }
        
        private async void LoadLatestBackieeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _logger.LogInformation("Load Latest Backiee Wallpapers button clicked");
                
                // Show loading indicator
                if (FindName("LoadingProgressRing") is Wpf.Ui.Controls.ProgressRing progressRing)
                {
                    progressRing.Visibility = Visibility.Visible;
                }
                
                // Show a snackbar notification
                _snackbarService.Show("Info", "Fetching latest wallpapers from Backiee...", 
                    ControlAppearance.Info, null, TimeSpan.FromSeconds(2));
                
                // Load the latest wallpapers from Backiee
                await FetchLatestWallpapersFromBackieeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading latest wallpapers from Backiee");
                
                try
                {
                    // Use multiple placeholder wallpapers as fallback (at least 10)
                    var placeholderModels = _backieeScraperService.GeneratePlaceholderWallpapers(10);
                    
                    if (placeholderModels.Any())
                    {
                        var wallpapers = new List<Wallpaper>();
                        
                        // Convert all placeholder models to Wallpaper objects
                        foreach (var model in placeholderModels)
                        {
                            var wallpaper = new Wallpaper
                            {
                                Id = model.Id,
                                Title = model.Title,
                                Name = model.Title,
                                Tags = new List<string> { "Latest" },
                                ThumbnailUrl = model.ThumbnailUrl,
                                SourceUrl = model.ImageUrl,
                                Width = model.Width,
                                Height = model.Height,
                                Source = Core.Models.WallpaperSource.Custom,
                                Metadata = new Dictionary<string, string>
                                {
                                    { "Source", "Backiee" },
                                    { "Rating", model.Rating.ToString() },
                                    { "Resolution", model.ResolutionCategory },
                                    { "IsPlaceholder", "true" }
                                }
                            };
                            
                            // Copy over any additional metadata
                            if (model.Metadata != null)
                            {
                                foreach (var kvp in model.Metadata)
                                {
                                    if (!wallpaper.Metadata.ContainsKey(kvp.Key))
                                    {
                                        wallpaper.Metadata.Add(kvp.Key, kvp.Value);
                                    }
                                }
                            }
                            
                            wallpapers.Add(wallpaper);
                        }
                        
                        // Update the WallpaperRepository with these placeholder wallpapers
                        foreach (var wallpaper in wallpapers)
                        {
                            await _wallpaperService.SaveWallpaperAsync(wallpaper);
                        }
                        
                        _logger.LogInformation("Added {Count} placeholder wallpapers with 'Latest' tag", wallpapers.Count);
                        
                        // Get a random placeholder for the featured image
                        var featuredWallpaper = wallpapers.FirstOrDefault();
                        
                        if (featuredWallpaper != null && FindName("FeaturedWallpaperImage") is System.Windows.Controls.Image featuredImage)
                        {
                            LoadPlaceholderImage(featuredImage, featuredWallpaper);
                        }
                        
                        _snackbarService.Show("Notice", "Using placeholders while trying to fetch wallpapers", 
                            ControlAppearance.Caution, null, TimeSpan.FromSeconds(3));
                            
                        // Navigate to the Latest category to show all placeholder wallpapers
                        NavigateToCategory("Latest");
                    }
                }
                catch (Exception innerEx)
                {
                    _logger.LogError(innerEx, "Error creating placeholder wallpapers after initial error");
                }
                
                _snackbarService.Show("Error", "Failed to load latest wallpapers", 
                    ControlAppearance.Danger, null, TimeSpan.FromSeconds(2));
            }
            finally
            {
                // Hide loading indicator
                if (FindName("LoadingProgressRing") is Wpf.Ui.Controls.ProgressRing progressRing)
                {
                    progressRing.Visibility = Visibility.Collapsed;
                }
            }
        }
        
        private async Task FetchLatestWallpapersFromBackieeAsync()
        {
            Wpf.Ui.Controls.ProgressRing loadingRing = null;
            if (FindName("LoadingProgressRing") is Wpf.Ui.Controls.ProgressRing ring)
            {
                loadingRing = ring;
                loadingRing.Visibility = Visibility.Visible;
            }
            
            try
            {
                _logger.LogInformation("Fetching latest wallpapers from Backiee");
                
                // Try to fetch actual wallpapers from Backiee
                var wallpaperModels = await _backieeScraperService.ScrapeLatestWallpapers();
                
                if (wallpaperModels.Any())
                {
                    _logger.LogInformation("Successfully scraped {Count} wallpapers from Backiee", wallpaperModels.Count);
                    
                    var wallpapers = new List<Wallpaper>();
                    
                    // Convert from WallpaperModel to Wallpaper
                    foreach (var model in wallpaperModels)
                    {
                        var wallpaper = new Wallpaper
                        {
                            Id = model.Id,
                            Title = model.Title,
                            Name = model.Title,
                            Tags = new List<string> { "Latest" },
                            ThumbnailUrl = model.ThumbnailUrl,
                            SourceUrl = model.ImageUrl,
                            Width = model.Width,
                            Height = model.Height,
                            Source = Core.Models.WallpaperSource.Custom,
                            Metadata = new Dictionary<string, string>
                            {
                                { "Source", "Backiee" },
                                { "Rating", model.Rating.ToString() },
                                { "Resolution", model.ResolutionCategory }
                            }
                        };
                        
                        wallpapers.Add(wallpaper);
                        
                        // Save each wallpaper
                        await _wallpaperService.SaveWallpaperAsync(wallpaper);
                    }
                    
                    _logger.LogInformation("Added {Count} wallpapers with 'Latest' tag", wallpapers.Count);
                    
                    // Get the highest rated wallpaper for featured display
                    var featuredWallpaper = wallpapers.OrderByDescending(w => 
                        w.Metadata != null && w.Metadata.TryGetValue("Rating", out var rating) 
                        ? int.Parse(rating) : 0).FirstOrDefault();
                    
                    if (featuredWallpaper != null && FindName("FeaturedWallpaperImage") is System.Windows.Controls.Image featuredImage)
                    {
                        await LoadImageFromUrl(featuredWallpaper.SourceUrl, featuredImage);
                    }
                    
                    _snackbarService.Show("Success", "Latest wallpapers loaded successfully", 
                        ControlAppearance.Success, null, TimeSpan.FromSeconds(2));
                    
                    // Navigate to the Latest category to show all wallpapers
                    NavigateToCategory("Latest");
                }
                else
                {
                    _logger.LogWarning("No wallpapers returned from Backiee, using placeholder wallpapers");
                    
                    // Generate and use multiple placeholder wallpapers (at least 10)
                    var placeholderModels = _backieeScraperService.GeneratePlaceholderWallpapers(10);
                    
                    if (placeholderModels.Any())
                    {
                        var wallpapers = new List<Wallpaper>();
                        
                        // Convert all placeholder models to Wallpaper objects
                        foreach (var model in placeholderModels)
                        {
                            var wallpaper = new Wallpaper
                            {
                                Id = model.Id,
                                Title = model.Title,
                                Name = model.Title,
                                Tags = new List<string> { "Latest" },
                                ThumbnailUrl = model.ThumbnailUrl,
                                SourceUrl = model.ImageUrl,
                                Width = model.Width,
                                Height = model.Height,
                                Source = Core.Models.WallpaperSource.Custom,
                                Metadata = new Dictionary<string, string>
                                {
                                    { "Source", "Backiee" },
                                    { "Rating", model.Rating.ToString() },
                                    { "Resolution", model.ResolutionCategory },
                                    { "IsPlaceholder", "true" }
                                }
                            };
                            
                            // Copy over any additional metadata if present
                            if (model.Metadata != null)
                            {
                                foreach (var kvp in model.Metadata)
                                {
                                    if (!wallpaper.Metadata.ContainsKey(kvp.Key))
                                    {
                                        wallpaper.Metadata.Add(kvp.Key, kvp.Value);
                                    }
                                }
                            }
                            
                            wallpapers.Add(wallpaper);
                            
                            // Save each wallpaper
                            await _wallpaperService.SaveWallpaperAsync(wallpaper);
                        }
                        
                        _logger.LogInformation("Added {Count} placeholder wallpapers with 'Latest' tag", wallpapers.Count);
                        
                        // Get a random placeholder for the featured image
                        var featuredWallpaper = wallpapers.FirstOrDefault();
                        
                        if (featuredWallpaper != null && FindName("FeaturedWallpaperImage") is System.Windows.Controls.Image featuredImage)
                        {
                            LoadPlaceholderImage(featuredImage, featuredWallpaper);
                        }
                        
                        _snackbarService.Show("Info", "Using placeholder wallpapers for preview", 
                            ControlAppearance.Caution, null, TimeSpan.FromSeconds(3));
                            
                        // Navigate to the Latest category to show all placeholder wallpapers
                        NavigateToCategory("Latest");
                    }
                    else
                    {
                        _snackbarService.Show("Warning", "No wallpapers found from Backiee", 
                            ControlAppearance.Caution, null, TimeSpan.FromSeconds(2));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading latest wallpapers from Backiee");
                
                try
                {
                    // Use multiple placeholder wallpapers as fallback (at least 10)
                    var placeholderModels = _backieeScraperService.GeneratePlaceholderWallpapers(10);
                    
                    if (placeholderModels.Any())
                    {
                        var wallpapers = new List<Wallpaper>();
                        
                        // Convert all placeholder models to Wallpaper objects
                        foreach (var model in placeholderModels)
                        {
                            var wallpaper = new Wallpaper
                            {
                                Id = model.Id,
                                Title = model.Title,
                                Name = model.Title,
                                Tags = new List<string> { "Latest" },
                                ThumbnailUrl = model.ThumbnailUrl,
                                SourceUrl = model.ImageUrl,
                                Width = model.Width,
                                Height = model.Height,
                                Source = Core.Models.WallpaperSource.Custom,
                                Metadata = new Dictionary<string, string>
                                {
                                    { "Source", "Backiee" },
                                    { "Rating", model.Rating.ToString() },
                                    { "Resolution", model.ResolutionCategory },
                                    { "IsPlaceholder", "true" }
                                }
                            };
                            
                            // Copy over any additional metadata
                            if (model.Metadata != null)
                            {
                                foreach (var kvp in model.Metadata)
                                {
                                    if (!wallpaper.Metadata.ContainsKey(kvp.Key))
                                    {
                                        wallpaper.Metadata.Add(kvp.Key, kvp.Value);
                                    }
                                }
                            }
                            
                            wallpapers.Add(wallpaper);
                            
                            // Save each wallpaper
                            await _wallpaperService.SaveWallpaperAsync(wallpaper);
                        }
                        
                        _logger.LogInformation("Added {Count} placeholder wallpapers with 'Latest' tag", wallpapers.Count);
                        
                        // Get a random placeholder for the featured image
                        var featuredWallpaper = wallpapers.FirstOrDefault();
                        
                        if (featuredWallpaper != null && FindName("FeaturedWallpaperImage") is System.Windows.Controls.Image featuredImage)
                        {
                            LoadPlaceholderImage(featuredImage, featuredWallpaper);
                        }
                        
                        _snackbarService.Show("Notice", "Using placeholders while trying to fetch wallpapers", 
                            ControlAppearance.Caution, null, TimeSpan.FromSeconds(3));
                            
                        // Navigate to the Latest category to show all placeholder wallpapers
                        NavigateToCategory("Latest");
                    }
                }
                catch (Exception innerEx)
                {
                    _logger.LogError(innerEx, "Error creating placeholder wallpapers after initial error");
                }
                
                _snackbarService.Show("Error", "Failed to load latest wallpapers", 
                    ControlAppearance.Danger, null, TimeSpan.FromSeconds(2));
            }
            finally
            {
                // Hide loading indicator
                if (loadingRing != null)
                {
                    loadingRing.Visibility = Visibility.Collapsed;
                }
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
        
        private void FeaturedWallpaper_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                _logger.LogInformation("Featured wallpaper image clicked, navigating to Latest category");
                NavigateToCategory("Latest");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling featured wallpaper click");
                _snackbarService.Show("Error", "Failed to navigate to latest wallpapers", 
                    ControlAppearance.Danger, null, TimeSpan.FromSeconds(2));
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