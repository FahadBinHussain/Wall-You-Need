using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Wpf.Ui.Controls;
using WallYouNeed.Core.Models;
using WallYouNeed.Core.Services.Interfaces;
using WallYouNeed.Core.Services;
using WallYouNeed.App.Pages;

namespace WallYouNeed.App.Pages
{
    public partial class CategoryPage : Page
    {
        private readonly ILogger<CategoryPage> _logger;
        private readonly IWallpaperService _wallpaperService;
        private readonly Wpf.Ui.ISnackbarService _snackbarService;
        private readonly IBackieeScraperService _backieeScraperService;
        
        private string _categoryName = "Category";
        private List<Wallpaper> _wallpapers = new List<Wallpaper>();

        public CategoryPage(
            ILogger<CategoryPage> logger,
            IWallpaperService wallpaperService,
            Wpf.Ui.ISnackbarService snackbarService,
            IBackieeScraperService backieeScraperService)
        {
            _logger = logger;
            _wallpaperService = wallpaperService;
            _snackbarService = snackbarService;
            _backieeScraperService = backieeScraperService;
            
            InitializeComponent();
            
            // Subscribe to Loaded event to add buttons after the UI is initialized
            this.Loaded += OnPageLoaded;
        }
        
        private void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Find the Actions StackPanel defined in XAML
                var sortButton = FindName("SortButton") as Wpf.Ui.Controls.Button;
                StackPanel actionsPanel = sortButton?.Parent as StackPanel;
                
                if (actionsPanel != null)
                {
                    // Add a separator
                    actionsPanel.Children.Add(new Separator 
                    { 
                        Margin = new Thickness(8, 0, 8, 0),
                        VerticalAlignment = VerticalAlignment.Stretch,
                        Background = new System.Windows.Media.SolidColorBrush(
                            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#20808080"))
                    });
                    
                    // Filter button - using simple button without icon to avoid ambiguity
                    var filterButton = new Wpf.Ui.Controls.Button
                    {
                        Content = "Filter",
                        Margin = new Thickness(8, 0, 0, 0)
                    };
                    filterButton.Click += FilterButton_Click;
                    actionsPanel.Children.Add(filterButton);
                    
                    // Slideshow button - using simple button without icon to avoid ambiguity
                    var slideshowButton = new Wpf.Ui.Controls.Button
                    {
                        Content = "Set as slideshow",
                        Margin = new Thickness(8, 0, 0, 0)
                    };
                    slideshowButton.Click += SlideshowButton_Click;
                    actionsPanel.Children.Add(slideshowButton);
                    
                    _logger.LogInformation("Added filter and slideshow buttons to Actions panel");
                }
                else
                {
                    _logger.LogWarning("Actions panel not found in XAML, couldn't add filter and slideshow buttons");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding action buttons");
            }
        }
        
        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _logger.LogInformation("Filter button clicked for category: {CategoryName}", _categoryName);
                
                // Placeholder for filter functionality
                _snackbarService.Show("Filter", "Filter functionality will be implemented soon", 
                    Wpf.Ui.Controls.ControlAppearance.Info, null, TimeSpan.FromSeconds(2));
                
                // TODO: Implement filtering dialog or panel
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling filter button click");
            }
        }
        
        private void SlideshowButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _logger.LogInformation("Slideshow button clicked for category: {CategoryName}", _categoryName);
                
                // Placeholder for slideshow functionality
                _snackbarService.Show("Slideshow", "Slideshow functionality will be implemented soon", 
                    Wpf.Ui.Controls.ControlAppearance.Info, null, TimeSpan.FromSeconds(2));
                
                // TODO: Implement slideshow functionality
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling slideshow button click");
            }
        }
        
        public async void SetCategory(string categoryName)
        {
            _categoryName = categoryName;
            CategoryTitle.Text = categoryName;
            CategoryDescription.Text = $"Browse and manage {categoryName} wallpapers";
            
            _logger.LogInformation("Category page set to: {CategoryName}", categoryName);
            
            // Load wallpapers for this category
            await LoadWallpapersForCategory(categoryName);
        }
        
        private async Task LoadWallpapersForCategory(string categoryName)
        {
            try
            {
                LoadingSpinner.Visibility = Visibility.Visible;
                NoWallpapersMessage.Visibility = Visibility.Collapsed;
                WallpapersPanel.Children.Clear();
                
                List<Wallpaper> wallpapers;
                
                // Special handling for different categories
                if (categoryName.Equals("Latest", StringComparison.OrdinalIgnoreCase))
                {
                    wallpapers = await FetchLatestWallpapersFromBackieeAsync();
                }
                else if (categoryName.Equals("Backiee Content", StringComparison.OrdinalIgnoreCase))
                {
                    wallpapers = await LoadWallpapersFromBackieeContentHtml();
                }
                else
                {
                    // Get wallpapers by tag for other categories
                    wallpapers = (await _wallpaperService.GetWallpapersByTagAsync(categoryName)).ToList();
                }
                
                _wallpapers = wallpapers;
                
                if (_wallpapers.Count == 0)
                {
                    NoWallpapersMessage.Visibility = Visibility.Visible;
                }
                else
                {
                    // Display wallpapers in the UI
                    DisplayWallpapers();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading wallpapers for category: {CategoryName}", categoryName);
                _snackbarService.Show("Error", "Failed to load wallpapers", 
                    Wpf.Ui.Controls.ControlAppearance.Danger, null, TimeSpan.FromSeconds(2));
            }
            finally
            {
                LoadingSpinner.Visibility = Visibility.Collapsed;
            }
        }
        
        private async Task<List<Wallpaper>> FetchLatestWallpapersFromBackieeAsync()
        {
            _logger.LogInformation("Fetching latest wallpapers from backiee.com");
            
            var wallpapers = new List<Wallpaper>();
            
            try
            {
                // First, try to get the latest wallpapers from the scraper service
                var wallpaperModels = await _backieeScraperService.ScrapeLatestWallpapers();
                
                if (wallpaperModels != null && wallpaperModels.Any())
                {
                    _logger.LogInformation("Successfully fetched {Count} wallpapers from BackieeScraperService", wallpaperModels.Count);
                    
                    // Convert WallpaperModel objects to Wallpaper objects
                    foreach (var model in wallpaperModels)
                    {
                        var wallpaper = ConvertToWallpaper(model);
                        wallpapers.Add(wallpaper);
                    }
                    
                    // Save the fetched wallpapers to the wallpaper service
                    await SaveWallpapersToService(wallpapers);
                }
                else
                {
                    _logger.LogWarning("No wallpapers returned from BackieeScraperService, trying hardcoded wallpapers");
                    
                    // Try to get hardcoded wallpapers as a fallback
                    var hardcodedWallpapers = await _backieeScraperService.GetHardcodedWallpapers();
                    
                    if (hardcodedWallpapers != null && hardcodedWallpapers.Any())
                    {
                        _logger.LogInformation("Successfully fetched {Count} hardcoded wallpapers", hardcodedWallpapers.Count);
                        
                        // Convert WallpaperModel objects to Wallpaper objects
                        foreach (var model in hardcodedWallpapers)
                        {
                            var wallpaper = ConvertToWallpaper(model);
                            wallpapers.Add(wallpaper);
                        }
                        
                        // Save the fetched wallpapers to the wallpaper service
                        await SaveWallpapersToService(wallpapers);
                    }
                    else
                    {
                        _logger.LogWarning("No hardcoded wallpapers found, using static placeholder wallpapers");
                        
                        // Use static placeholder wallpapers instead of dynamic ones
                        var placeholderModels = _backieeScraperService.GeneratePlaceholderWallpapers(12);
                        
                        foreach (var model in placeholderModels)
                        {
                            var wallpaper = ConvertToWallpaper(model);
                            wallpapers.Add(wallpaper);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching wallpapers from backiee.com, using static placeholder wallpapers");
                
                // Use placeholder wallpapers as a fallback
                var placeholderModels = _backieeScraperService.GeneratePlaceholderWallpapers(8);
                
                foreach (var model in placeholderModels)
                {
                    var wallpaper = ConvertToWallpaper(model);
                    wallpapers.Add(wallpaper);
                }
            }
            
            return wallpapers;
        }
        
        // Helper method to convert WallpaperModel to Wallpaper
        private Wallpaper ConvertToWallpaper(WallpaperModel model)
        {
            var wallpaper = new Wallpaper
            {
                Id = model.Id,
                Title = model.Title,
                Name = model.Title,
                Tags = new List<string> { model.Category, "Latest" },
                ThumbnailUrl = model.ThumbnailUrl,
                SourceUrl = model.ImageUrl,
                Width = model.Width,
                Height = model.Height,
                Source = WallpaperSource.Custom,
                CreatedAt = model.UploadDate,
                Metadata = new Dictionary<string, string>
                {
                    { "Source", "Backiee" },
                    { "Rating", model.Rating.ToString() },
                    { "Resolution", model.ResolutionCategory ?? "4K" }
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
            
            return wallpaper;
        }
        
        // Helper method to save wallpapers to the service
        private async Task SaveWallpapersToService(List<Wallpaper> wallpapers)
        {
            try
            {
                foreach (var wallpaper in wallpapers)
                {
                    await _wallpaperService.SaveWallpaperAsync(wallpaper);
                }
                _logger.LogInformation("Successfully saved {Count} wallpapers to service", wallpapers.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving wallpapers to service");
            }
        }
        
        private void DisplayWallpapers()
        {
            foreach (var wallpaper in _wallpapers)
            {
                var card = CreateWallpaperCard(wallpaper);
                WallpapersPanel.Children.Add(card);
            }
        }
        
        private UIElement CreateWallpaperCard(Wallpaper wallpaper)
        {
            // Create the card container
            var card = new Card
            {
                Margin = new Thickness(8),
                Width = 280,
                Height = 200
            };
            
            var grid = new Grid();
            card.Content = grid;
            
            // Add the wallpaper image
            var image = new System.Windows.Controls.Image
            {
                Stretch = System.Windows.Media.Stretch.UniformToFill
            };
            
            // Load the image (this could be a local file or from a URL)
            LoadImageSource(image, wallpaper);
            
            grid.Children.Add(image);
            
            // Add resolution badge (top-left corner)
            var resolutionBadge = CreateResolutionBadge(wallpaper);
            grid.Children.Add(resolutionBadge);
            
            // Add like/download counts (bottom-right corner)
            var statsOverlay = CreateStatsOverlay(wallpaper);
            grid.Children.Add(statsOverlay);
            
            // Add bottom info overlay with controls
            var infoOverlay = CreateInfoOverlay(wallpaper);
            grid.Children.Add(infoOverlay);
            
            // Handle click event for the card
            card.MouseLeftButtonUp += (s, e) => WallpaperCard_Click(wallpaper);
            
            return card;
        }
        
        private UIElement CreateResolutionBadge(Wallpaper wallpaper)
        {
            string resType;
            
            // Check if resolution is specified in metadata
            if (wallpaper.Metadata != null && wallpaper.Metadata.TryGetValue("Resolution", out var metadataRes))
            {
                resType = metadataRes;
            }
            else
            {
                // Calculate resolution from dimensions
                resType = DetermineResolutionType(wallpaper.Width, wallpaper.Height);
            }
            
            // Create border for the badge
            var border = new Border
            {
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(12, 12, 0, 0),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Thickness(6, 2, 6, 2)
            };
            
            // Set background color based on resolution type
            switch (resType)
            {
                case "8K":
                    border.Background = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#7B1FA2"));
                    break;
                case "5K":
                    border.Background = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E91E63"));
                    break;
                case "4K":
                    border.Background = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2196F3"));
                    break;
                case "AI":
                    border.Background = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#673AB7"));
                    break;
                default:
                    border.Background = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#607D8B"));
                    break;
            }
            
            // Add resolution text
            var textBlock = new System.Windows.Controls.TextBlock
            {
                Text = resType,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White),
                FontSize = 12,
                FontWeight = FontWeights.Bold
            };
            
            border.Child = textBlock;
            return border;
        }
        
        private UIElement CreateStatsOverlay(Wallpaper wallpaper)
        {
            // Get like/download stats from metadata if available
            int likes = 0;
            int downloads = 0;
            
            if (wallpaper.Metadata != null)
            {
                if (wallpaper.Metadata.TryGetValue("Likes", out var likesStr))
                {
                    int.TryParse(likesStr, out likes);
                }
                
                if (wallpaper.Metadata.TryGetValue("Downloads", out var downloadsStr))
                {
                    int.TryParse(downloadsStr, out downloads);
                }
            }
            
            // If no metadata is available, use random values as fallback
            if (likes == 0)
            {
                likes = new Random().Next(1, 60);
            }
            
            if (downloads == 0)
            {
                downloads = new Random().Next(10, 300);
            }
            
            var border = new Border
            {
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 12, 12)
            };
            
            var sp = new StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal
            };
            
            // Likes counter
            var likesContainer = new StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                Margin = new Thickness(0, 0, 8, 0)
            };
            
            var heartIcon = new System.Windows.Controls.TextBlock
            {
                Text = "♥",
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White),
                FontSize = 14,
                Margin = new Thickness(0, 0, 4, 0)
            };
            
            var likesCount = new System.Windows.Controls.TextBlock
            {
                Text = likes.ToString(),
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White),
                FontSize = 14
            };
            
            likesContainer.Children.Add(heartIcon);
            likesContainer.Children.Add(likesCount);
            
            // Downloads counter
            var downloadsContainer = new StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal
            };
            
            var downloadIcon = new System.Windows.Controls.TextBlock
            {
                Text = "↓",
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White),
                FontSize = 14,
                Margin = new Thickness(0, 0, 4, 0)
            };
            
            var downloadsCount = new System.Windows.Controls.TextBlock
            {
                Text = downloads.ToString(),
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White),
                FontSize = 14
            };
            
            downloadsContainer.Children.Add(downloadIcon);
            downloadsContainer.Children.Add(downloadsCount);
            
            sp.Children.Add(likesContainer);
            sp.Children.Add(downloadsContainer);
            
            border.Child = sp;
            return border;
        }
        
        private UIElement CreateInfoOverlay(Wallpaper wallpaper)
        {
            // Create a semi-transparent overlay at the bottom
            var border = new Border
            {
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(176, 0, 0, 0)), // #B0000000
                VerticalAlignment = VerticalAlignment.Bottom,
                Padding = new Thickness(10, 8, 10, 8),
                Height = 0 // Initially hidden
            };
            
            var sp = new StackPanel();
            
            // Wallpaper title
            var title = new System.Windows.Controls.TextBlock
            {
                Text = wallpaper.Name,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White),
                FontSize = 14,
                FontWeight = FontWeights.SemiBold
            };
            
            // Source info
            var source = new System.Windows.Controls.TextBlock
            {
                Text = $"Source: {wallpaper.Source}",
                Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#CCCCCC")),
                FontSize = 12,
                Margin = new Thickness(0, 4, 0, 0)
            };
            
            // Action buttons
            var buttonsPanel = new StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                Margin = new Thickness(0, 8, 0, 0)
            };
            
            var applyButton = new Wpf.Ui.Controls.Button
            {
                Content = "Apply",
                Appearance = ControlAppearance.Primary,
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 4, 0),
                FontSize = 12
            };
            applyButton.Click += (s, e) => ApplyWallpaper_Click(wallpaper);
            
            var favoriteButton = new Wpf.Ui.Controls.Button
            {
                Content = "♡",
                Appearance = ControlAppearance.Secondary,
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(4, 0, 4, 0),
                FontSize = 12
            };
            favoriteButton.Click += (s, e) => FavoriteWallpaper_Click(wallpaper);
            
            var menuButton = new Wpf.Ui.Controls.Button
            {
                Content = "⋮",
                Appearance = ControlAppearance.Secondary,
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(4, 0, 0, 0),
                FontSize = 12
            };
            menuButton.Click += (s, e) => MenuButton_Click(wallpaper);
            
            buttonsPanel.Children.Add(applyButton);
            buttonsPanel.Children.Add(favoriteButton);
            buttonsPanel.Children.Add(menuButton);
            
            sp.Children.Add(title);
            sp.Children.Add(source);
            sp.Children.Add(buttonsPanel);
            
            border.Child = sp;
            
            // Handle mouse enter/leave to show/hide the overlay
            var parentCard = new Card(); // This will be set properly when the method is called
            
            return border;
        }
        
        private void LoadImageSource(System.Windows.Controls.Image image, Wallpaper wallpaper)
        {
            try
            {
                // Default to an error handler in case image loading fails
                image.ImageFailed += (s, e) =>
                {
                    _logger.LogWarning("Image loading failed for wallpaper: {WallpaperId}, trying fallback", wallpaper.Id);
                    TryLoadFallbackImage(image, wallpaper);
                };
                
                if (!string.IsNullOrEmpty(wallpaper.FilePath) && File.Exists(wallpaper.FilePath))
                {
                    // Load from local file
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(wallpaper.FilePath);
                    bitmap.EndInit();
                    
                    image.Source = bitmap;
                }
                else if (!string.IsNullOrEmpty(wallpaper.ThumbnailUrl))
                {
                    // Use thumbnail if available
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(wallpaper.ThumbnailUrl);
                    bitmap.EndInit();
                    
                    image.Source = bitmap;
                }
                else if (!string.IsNullOrEmpty(wallpaper.SourceUrl))
                {
                    // Load from source URL as fallback
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(wallpaper.SourceUrl);
                    bitmap.EndInit();
                    
                    image.Source = bitmap;
                }
                else
                {
                    // No valid image source found, use fallback method
                    TryLoadFallbackImage(image, wallpaper);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading image for wallpaper: {WallpaperId}", wallpaper.Id);
                TryLoadFallbackImage(image, wallpaper);
            }
        }
        
        private void TryLoadFallbackImage(System.Windows.Controls.Image image, Wallpaper wallpaper)
        {
            try
            {
                _logger.LogWarning("Using fallback image for wallpaper: {WallpaperId}", wallpaper.Id);
                
                // Try to load directly from thumbnail URL first
                if (!string.IsNullOrEmpty(wallpaper.ThumbnailUrl))
                {
                    try
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.UriSource = new Uri(wallpaper.ThumbnailUrl);
                        bitmap.EndInit();
                        
                        image.Source = bitmap;
                        _logger.LogInformation("Successfully loaded fallback from thumbnail URL: {Url}", wallpaper.ThumbnailUrl);
                        return;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error loading thumbnail URL in fallback: {Url}", wallpaper.ThumbnailUrl);
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
                        
                        image.Source = bitmap;
                        _logger.LogInformation("Successfully loaded fallback from source URL: {Url}", wallpaper.SourceUrl);
                        return;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error loading source URL in fallback: {Url}", wallpaper.SourceUrl);
                    }
                }
                
                // If all loading attempts failed, just show a simple "No Image" text
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
                        new System.Windows.Rect(0, 0, 280, 200));
                    
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
                        (200 - text.Height) / 2));
                }
                
                // Convert drawing to bitmap
                var renderTarget = new System.Windows.Media.Imaging.RenderTargetBitmap(
                    280, 200, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                renderTarget.Render(drawingVisual);
                
                // Set as image source
                image.Source = renderTarget;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating fallback image for wallpaper: {WallpaperId}", wallpaper.Id);
            }
        }
        
        private string DetermineResolutionType(int width, int height)
        {
            int pixels = width * height;
            
            if (pixels >= 33177600) // 7680x4320
                return "8K";
            else if (pixels >= 14745600) // 5120x2880
                return "5K";
            else if (pixels >= 8294400) // 3840x2160
                return "4K";
            else if (pixels >= 2073600) // 1920x1080
                return "FHD";
            else if (pixels >= 921600) // 1280x720
                return "HD";
            else
                return "SD";
        }
        
        private void WallpaperCard_Click(Wallpaper wallpaper)
        {
            _logger.LogInformation("Wallpaper card clicked: {WallpaperId}", wallpaper.Id);
            // TODO: Implement wallpaper detail view
        }
        
        private async void ApplyWallpaper_Click(Wallpaper wallpaper)
        {
            try
            {
                _logger.LogInformation("Applying wallpaper: {WallpaperId}", wallpaper.Id);
                
                bool success = await _wallpaperService.ApplyWallpaperAsync(wallpaper.Id);
                
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
                _logger.LogError(ex, "Error applying wallpaper: {WallpaperId}", wallpaper.Id);
                _snackbarService.Show("Error", $"Error applying wallpaper: {ex.Message}", 
                    ControlAppearance.Danger, null, TimeSpan.FromSeconds(2));
            }
        }
        
        private async void FavoriteWallpaper_Click(Wallpaper wallpaper)
        {
            try
            {
                _logger.LogInformation("Toggling favorite for wallpaper: {WallpaperId}", wallpaper.Id);
                
                bool success = await _wallpaperService.ToggleFavoriteAsync(wallpaper.Id);
                
                if (success)
                {
                    _snackbarService.Show("Success", "Favorite status updated", 
                        ControlAppearance.Success, null, TimeSpan.FromSeconds(2));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling favorite for wallpaper: {WallpaperId}", wallpaper.Id);
                _snackbarService.Show("Error", $"Error updating favorite status: {ex.Message}", 
                    ControlAppearance.Danger, null, TimeSpan.FromSeconds(2));
            }
        }
        
        private void MenuButton_Click(Wallpaper wallpaper)
        {
            _logger.LogInformation("Menu clicked for wallpaper: {WallpaperId}", wallpaper.Id);
            // TODO: Implement menu options (download, share, etc.)
        }
        
        private void SortButton_Click(object sender, RoutedEventArgs e)
        {
            // Placeholder for future implementation
            System.Windows.MessageBox.Show(
                "Sort functionality not implemented yet.",
                "Not Implemented",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }
        
        private void AddWallpaperButton_Click(object sender, RoutedEventArgs e)
        {
            // Placeholder for future implementation
            System.Windows.MessageBox.Show(
                "Add wallpaper functionality not implemented yet.",
                "Not Implemented",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }
        
        private async Task<List<Wallpaper>> LoadWallpapersFromBackieeContentHtml()
        {
            try
            {
                _logger.LogInformation("Loading wallpapers from backiee_content.html");
                
                // Open file dialog to let user select the backiee_content.html file
                var openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Select backiee_content.html file",
                    Filter = "HTML files (*.html)|*.html",
                    Multiselect = false
                };
                
                if (openFileDialog.ShowDialog() != true)
                {
                    _logger.LogInformation("User cancelled the file selection");
                    return new List<Wallpaper>();
                }
                
                string filePath = openFileDialog.FileName;
                _logger.LogInformation("Selected file: {FilePath}", filePath);
                
                // Get an instance of HtmlDownloader to load the file
                var htmlDownloader = new WallYouNeed.Core.Utils.HtmlDownloader(
                    new System.Net.Http.HttpClient(),
                    Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<ILogger<WallYouNeed.Core.Utils.HtmlDownloader>>(
                        ((App)System.Windows.Application.Current).Services)
                );
                
                // Load the HTML content from the file
                string htmlContent = await htmlDownloader.LoadHtmlFromFileAsync(filePath);
                
                if (string.IsNullOrEmpty(htmlContent))
                {
                    _logger.LogWarning("Failed to load HTML content from file");
                    _snackbarService.Show("Error", "Failed to load HTML content from file", 
                        Wpf.Ui.Controls.ControlAppearance.Danger, null, TimeSpan.FromSeconds(2));
                    return new List<Wallpaper>();
                }
                
                _logger.LogInformation("Successfully loaded HTML content, length: {Length} characters", htmlContent.Length);
                
                // Check for placeholder divs in the HTML
                bool containsPlaceholderDivs = htmlContent.Contains("class=\"placeholder\"");
                bool containsLazyLoadImages = htmlContent.Contains("class=\"rounded-image img-fluid lazyload\"");
                
                _logger.LogInformation("HTML contains placeholder divs: {ContainsPlaceholderDivs}", containsPlaceholderDivs);
                _logger.LogInformation("HTML contains lazyload images: {ContainsLazyLoadImages}", containsLazyLoadImages);
                
                // Extract wallpapers from the content
                var wallpaperModels = await _backieeScraperService.ExtractWallpapersFromContentHtml(htmlContent);
                
                if (wallpaperModels == null || wallpaperModels.Count == 0)
                {
                    _logger.LogWarning("No wallpapers found in the HTML content");
                    _snackbarService.Show("No wallpapers", "No wallpapers found in the selected file. Make sure you're using a proper backiee.com HTML export.", 
                        Wpf.Ui.Controls.ControlAppearance.Caution, null, TimeSpan.FromSeconds(3));
                    return new List<Wallpaper>();
                }
                
                _logger.LogInformation("Found {Count} wallpapers in the HTML content", wallpaperModels.Count);
                
                // Log details of the first few wallpapers for debugging
                for (int i = 0; i < Math.Min(3, wallpaperModels.Count); i++)
                {
                    var model = wallpaperModels[i];
                    _logger.LogInformation("Wallpaper {Index}: ID={Id}, Title={Title}, URL={Url}", 
                        i+1, model.Id, model.Title, model.ImageUrl);
                }
                
                // Convert WallpaperModel to Wallpaper entities
                var wallpapers = new List<Wallpaper>();
                foreach (var model in wallpaperModels)
                {
                    try
                    {
                        wallpapers.Add(new Wallpaper
                        {
                            Id = model.Id,
                            Title = model.Title,
                            Name = model.Title,
                            SourceUrl = model.ImageUrl,
                            ThumbnailUrl = model.ThumbnailUrl,
                            Source = WallpaperSource.Custom,
                            Width = model.Width,
                            Height = model.Height,
                            Tags = new List<string> { "Backiee Content", model.ResolutionCategory },
                            Metadata = new Dictionary<string, string>
                            {
                                { "Resolution", model.ResolutionCategory },
                                { "Source", "backiee.com" },
                                { "ID", model.Id }
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error converting wallpaper model to Wallpaper entity: {Title}", model.Title);
                    }
                }
                
                _snackbarService.Show("Success", $"Found {wallpapers.Count} wallpapers in the content", 
                    Wpf.Ui.Controls.ControlAppearance.Success, null, TimeSpan.FromSeconds(2));
                
                return wallpapers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading wallpapers from backiee_content.html");
                _snackbarService.Show("Error", $"Failed to load wallpapers: {ex.Message}", 
                    Wpf.Ui.Controls.ControlAppearance.Danger, null, TimeSpan.FromSeconds(2));
                return new List<Wallpaper>();
            }
        }
    }
}