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
                
                // Special handling for "Latest" category - fetch from backiee.com
                if (categoryName.Equals("Latest", StringComparison.OrdinalIgnoreCase))
                {
                    wallpapers = await FetchLatestWallpapersFromBackieeAsync();
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
                // Use the injected BackieeScraperService to get real wallpapers
                if (_backieeScraperService != null)
                {
                    _logger.LogInformation("Using BackieeScraperService to fetch latest wallpapers");
                    
                    // Fetch the latest wallpapers from backiee.com
                    var backieeWallpapers = await _backieeScraperService.ScrapeLatestWallpapers();
                    
                    if (backieeWallpapers == null || !backieeWallpapers.Any())
                    {
                        _logger.LogWarning("No wallpapers returned from BackieeScraperService");
                        
                        // Show a snackbar notification
                        System.Windows.Application.Current.Dispatcher.Invoke(() => 
                        {
                            _snackbarService.Show("Warning", "No wallpapers found from backiee.com. Using fallback data.",
                                Wpf.Ui.Controls.ControlAppearance.Caution, null, TimeSpan.FromSeconds(3));
                        });
                        
                        // Return fallback wallpapers
                        return GetFallbackWallpapers();
                    }
                    
                    _logger.LogInformation("Successfully fetched {Count} wallpapers from backiee.com", backieeWallpapers.Count);
                    
                    // Convert WallpaperModel to Wallpaper
                    foreach (var model in backieeWallpapers)
                    {
                        try
                        {
                            wallpapers.Add(new Wallpaper
                            {
                                Id = Guid.NewGuid().ToString(),
                                Name = model.Title,
                                Title = model.Title,
                                SourceUrl = model.ImageUrl,
                                ThumbnailUrl = model.ThumbnailUrl,
                                Source = WallpaperSource.Custom,
                                Width = model.Width,
                                Height = model.Height,
                                Tags = new List<string> { "Latest", model.ResolutionCategory },
                                Metadata = new Dictionary<string, string>
                                {
                                    { "Resolution", model.ResolutionCategory },
                                    { "Source", "backiee.com" },
                                    { "Likes", new Random().Next(1, 60).ToString() }, // Backiee doesn't expose likes in the scraper
                                    { "Downloads", new Random().Next(10, 300).ToString() } // Backiee doesn't expose downloads in the scraper
                                }
                            });
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error converting wallpaper model to Wallpaper object");
                        }
                    }
                    
                    _logger.LogInformation("Successfully converted {Count} latest wallpapers from backiee.com", wallpapers.Count);
                }
                else
                {
                    _logger.LogWarning("BackieeScraperService not available, using placeholder data");
                    
                    // Return fallback wallpapers
                    return GetFallbackWallpapers();
                }
                
                return wallpapers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching latest wallpapers from backiee.com");
                
                // Show a snackbar notification
                System.Windows.Application.Current.Dispatcher.Invoke(() => 
                {
                    _snackbarService.Show("Error", "Failed to fetch latest wallpapers. Using fallback data.",
                        Wpf.Ui.Controls.ControlAppearance.Danger, null, TimeSpan.FromSeconds(3));
                });
                
                // Return fallback wallpapers on error
                return GetFallbackWallpapers();
            }
        }
        
        private List<Wallpaper> GetFallbackWallpapers()
        {
            var fallbackWallpapers = new List<Wallpaper>();
            
            _logger.LogInformation("Using fallback wallpaper data");
            
            // Fallback to placeholder data
            fallbackWallpapers.Add(CreateWallpaperFromBackiee(
                "Tiger Warrior Amidst Blazing Flames", 
                "https://wallpaper-house.com/data/out/12/wallpaper2you_594262.jpg", 
                3840, 2160, "backiee.com", 56, 258, "4K"));
                
            fallbackWallpapers.Add(CreateWallpaperFromBackiee(
                "Colorful Brickwork Symphony in 4K Splendor", 
                "https://images.pexels.com/photos/1308624/pexels-photo-1308624.jpeg", 
                3840, 2160, "backiee.com", 8, 36, "4K"));
                
            fallbackWallpapers.Add(CreateWallpaperFromBackiee(
                "Ford Mustang Power Duo in Stunning Sunset", 
                "https://images.hdqwalls.com/wallpapers/ford-mustang-4k-2020-9z.jpg", 
                5120, 2880, "backiee.com", 8, 32, "5K"));
            
            fallbackWallpapers.Add(CreateWallpaperFromBackiee(
                "Mountain Landscape with Calm Lake Reflection", 
                "https://images.pexels.com/photos/2662116/pexels-photo-2662116.jpeg", 
                4096, 2730, "backiee.com", 12, 45, "4K"));
                
            fallbackWallpapers.Add(CreateWallpaperFromBackiee(
                "Vibrant Abstract Digital Art Creation", 
                "https://www.pixelstalk.net/wp-content/uploads/2016/05/Free-Download-Cool-Abstract-Wallpapers-HD.jpg", 
                3840, 2160, "backiee.com", 7, 28, "4K"));
                
            _logger.LogInformation("Created {Count} fallback wallpapers", fallbackWallpapers.Count);
            
            return fallbackWallpapers;
        }
        
        private Wallpaper CreateWallpaperFromBackiee(string name, string imageUrl, int width, int height, 
            string source, int likes, int downloads, string resolution)
        {
            // Use a thumbnail version if available, or generate one from the URL
            string thumbnailUrl = imageUrl.Replace(".jpg", "_thumb.jpg")
                .Replace(".png", "_thumb.png")
                .Replace(".jpeg", "_thumb.jpeg");
                
            return new Wallpaper
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                Title = name,
                SourceUrl = imageUrl,
                ThumbnailUrl = thumbnailUrl,
                Source = WallpaperSource.Custom,
                Width = width,
                Height = height,
                Tags = new List<string> { "Latest", resolution },
                Metadata = new Dictionary<string, string>
                {
                    { "Resolution", resolution },
                    { "Source", "backiee.com" },
                    { "Likes", likes.ToString() },
                    { "Downloads", downloads.ToString() }
                }
            };
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
                    // No valid image source found, use a placeholder
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
                // Try to use a placeholder based on resolution category
                string resolution = "Other";
                if (wallpaper.Metadata != null && wallpaper.Metadata.TryGetValue("Resolution", out var res))
                {
                    resolution = res;
                }
                
                // Use a solid color background with resolution text
                var drawingVisual = new System.Windows.Media.DrawingVisual();
                using (var drawingContext = drawingVisual.RenderOpen())
                {
                    // Set background color based on resolution
                    System.Windows.Media.Color bgColor;
                    switch (resolution)
                    {
                        case "8K":
                            bgColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#7B1FA2");
                            break;
                        case "5K":
                            bgColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E91E63");
                            break;
                        case "4K":
                            bgColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2196F3");
                            break;
                        default:
                            bgColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#607D8B");
                            break;
                    }
                    
                    // Draw background
                    drawingContext.DrawRectangle(
                        new System.Windows.Media.SolidColorBrush(bgColor),
                        null,
                        new Rect(0, 0, 280, 200));
                    
                    // Draw resolution text
                    var text = new System.Windows.Media.FormattedText(
                        resolution,
                        System.Globalization.CultureInfo.CurrentCulture,
                        System.Windows.FlowDirection.LeftToRight,
                        new System.Windows.Media.Typeface("Segoe UI"),
                        24,
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
                
                _logger.LogInformation("Set fallback image for wallpaper: {WallpaperId}", wallpaper.Id);
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
    }
}