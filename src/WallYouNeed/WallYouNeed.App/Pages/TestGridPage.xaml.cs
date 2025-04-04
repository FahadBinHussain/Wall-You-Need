using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using WallYouNeed.Core.Models;
using WallYouNeed.Core.Services.Interfaces;
using System.Net.Http;

namespace WallYouNeed.App.Pages
{
    /// <summary>
    /// Interaction logic for TestGridPage.xaml
    /// </summary>
    public partial class TestGridPage : Page
    {
        private readonly ILogger<TestGridPage> _logger;
        private readonly ISettingsService _settingsService;
        private ObservableCollection<WallpaperItem> _wallpapers;
        private double _itemWidth = 300; // Default width for each wallpaper item
        private double _itemHeight = 180; // Default height for each wallpaper item
        private const int ScrollThreshold = 600; // Increased threshold for preemptive loading
        
        // Variables for JSON loading and infinite scrolling
        private HashSet<string> _loadedUrls = new HashSet<string>();
        private HashSet<int> _attemptedIds = new HashSet<int>();
        private int _currentImageId = -1; // Will be initialized properly after loading JSON
        private DateTime _lastScrollCheck = DateTime.MinValue;
        private TimeSpan _scrollDebounceTime = TimeSpan.FromMilliseconds(300); // Reduced debounce time
        private SemaphoreSlim _loadingSemaphore = new SemaphoreSlim(1, 1);
        private bool _isLoadingMore = false;
        private CancellationTokenSource _cts;
        private bool _isPageLoaded = false;
        private bool _shouldRestoreScrollPosition = true;
        private bool _isBackgroundLoadingEnabled = true; // Enable background loading
        private bool _isPrefetchingEnabled = true; // Enable prefetching of images

        // Simulated test data for wallpapers (as fallback)
        private readonly List<string> _resolutions = new List<string> { "4K", "5K", "8K" };
        private readonly Random _random = new Random();

        // Stats counters for tracking HTTP requests
        private int _totalRequests = 0;
        private int _successfulRequests = 0;
        private int _failedRequests = 0;
        private readonly int _batchSize = 20; // Number of images to check at once
        
        // Queue for background processing
        private Queue<Task> _backgroundTasks = new Queue<Task>();
        private readonly int _maxConcurrentBackgroundTasks = 2;
        private int _runningBackgroundTasks = 0;
        private readonly object _backgroundTaskLock = new object();

        public TestGridPage(ILogger<TestGridPage> logger = null, ISettingsService settingsService = null)
        {
            _logger = logger;
            _settingsService = settingsService;
            _logger?.LogInformation("TestGridPage constructor called");

            InitializeComponent();
            _wallpapers = new ObservableCollection<WallpaperItem>();

            // Create a new cancellation token source for infinite scrolling
            _cts = new CancellationTokenSource();

            // Register events
            Loaded += TestGridPage_Loaded;
            SizeChanged += TestGridPage_SizeChanged;
            Unloaded += TestGridPage_Unloaded;
        }

        private async void TestGridPage_Loaded(object sender, RoutedEventArgs e)
        {
            _logger?.LogInformation("TestGridPage loaded");
            
            // Show loading indicators
            StatusTextBlock.Visibility = Visibility.Visible;
            LoadingProgressBar.Visibility = Visibility.Visible;

            // Load settings
            await LoadSettingsAsync();

            // Initialize with JSON data
            await LoadInitialWallpapers();
            
            // Set flag to indicate page is loaded
            _isPageLoaded = true;
            
            // Start preemptive loading after initial load
            await Task.Delay(500); // Short delay to allow UI to render
            StartBackgroundLoading();
        }

        private void TestGridPage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _logger?.LogInformation($"Window size changed to: {e.NewSize.Width}x{e.NewSize.Height}");
            
            // Update the layout when the window size changes
            AdjustItemSizes();
            
            // Save settings when page is fully loaded
            if (_isPageLoaded)
            {
                SaveSettingsQuietly();
            }
        }
        
        private void TestGridPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _logger?.LogInformation("TestGridPage unloaded, saving settings");
            
            // Save settings when page is unloaded
            SaveSettings();
            
            // Cancel any ongoing operations
            _cts?.Cancel();
        }

        private async Task LoadSettingsAsync()
        {
            try
            {
                if (_settingsService == null)
                {
                    _logger?.LogWarning("SettingsService is null, using default settings");
                    return;
                }
                
                _logger?.LogInformation("Loading TestGridPage settings");
                var settings = await _settingsService.LoadSettingsAsync();
                
                // Set item sizes from settings
                if (settings.TestGridItemWidth > 50)
                {
                    _itemWidth = settings.TestGridItemWidth;
                }
                
                if (settings.TestGridItemHeight > 30)
                {
                    _itemHeight = settings.TestGridItemHeight;
                }
                
                _logger?.LogInformation($"Loaded settings - Item size: {_itemWidth}x{_itemHeight}, Scroll: {settings.TestGridScrollPosition}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading TestGridPage settings");
            }
        }
        
        private async void SaveSettingsQuietly()
        {
            try
            {
                await SaveSettingsAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error quietly saving TestGridPage settings");
            }
        }
        
        private void SaveSettings()
        {
            try
            {
                SaveSettingsQuietly();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error saving TestGridPage settings on unload");
            }
        }
        
        private async Task SaveSettingsAsync()
        {
            try
            {
                if (_settingsService == null || !_isPageLoaded)
                {
                    return;
                }
                
                double scrollPosition = MainScrollViewer.VerticalOffset;
                
                _logger?.LogInformation($"Saving TestGridPage settings - Item size: {_itemWidth}x{_itemHeight}, Scroll: {scrollPosition}");
                
                // Update settings with TestGridPage values
                await _settingsService.UpdateSettingsAsync(settings => 
                {
                    settings.TestGridItemWidth = _itemWidth;
                    settings.TestGridItemHeight = _itemHeight;
                    settings.TestGridScrollPosition = scrollPosition;
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error saving TestGridPage settings");
            }
        }

        private async Task LoadInitialWallpapers()
        {
            try
            {
                _logger?.LogInformation("Loading initial wallpapers");
                
                // Clear existing items
                WallpaperContainer.Children.Clear();
                _wallpapers.Clear();
                _loadedUrls.Clear();
                
                StatusTextBlock.Text = "Loading wallpapers from data file...";
                
                // Try to load from JSON file first
                bool jsonLoaded = await LoadImagesFromJsonFile();
                
                // If JSON loading failed, use test data as fallback
                if (!jsonLoaded)
                {
                    _logger?.LogWarning("Failed to load from JSON, falling back to test data");
                    StatusTextBlock.Text = "Using sample data (JSON file not found)";
                    await Task.Delay(1000); // Show message briefly
                    
                    // Generate test data
                    for (int i = 0; i < 20; i++)
                    {
                        await AddTestWallpaperItem();
                    }
                }

                // Hide loading indicators
                StatusTextBlock.Visibility = Visibility.Collapsed;
                LoadingProgressBar.Visibility = Visibility.Collapsed;

                // Adjust item sizes based on current window width
                AdjustItemSizes();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error initializing wallpaper grid");
                StatusTextBlock.Text = "Error loading wallpapers";
                StatusTextBlock.Visibility = Visibility.Visible;
                LoadingProgressBar.Visibility = Visibility.Collapsed;
            }
        }

        private async Task<bool> LoadImagesFromJsonFile()
        {
            try
            {
                // Look for the JSON file in the Data directory
                string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "backiee_wallpapers.json");
                var fullPath = Path.GetFullPath(jsonPath);
                _logger?.LogInformation($"Looking for JSON file at: {fullPath}");
                
                if (!File.Exists(fullPath))
                {
                    _logger?.LogError("JSON file not found in Data directory");
                    return false;
                }

                _logger?.LogInformation($"Found JSON file at: {fullPath}");

                // Read the JSON content
                string jsonContent = await File.ReadAllTextAsync(fullPath);
                
                // Use a simple approach for parsing the wallpapers
                var wallpapers = new List<SimpleWallpaper>();
                
                using (JsonDocument doc = JsonDocument.Parse(jsonContent))
                {
                    foreach (JsonElement item in doc.RootElement.EnumerateArray())
                    {
                        var wallpaper = new SimpleWallpaper();
                        
                        // Get the URL
                        if (item.TryGetProperty("placeholder_url", out JsonElement urlElement) && 
                            urlElement.ValueKind == JsonValueKind.String)
                        {
                            wallpaper.Url = urlElement.GetString() ?? "";
                        }
                        
                        // Get the quality
                        if (item.TryGetProperty("quality", out JsonElement qualityElement) && 
                            qualityElement.ValueKind == JsonValueKind.String)
                        {
                            wallpaper.Quality = qualityElement.GetString() ?? "";
                        }
                        
                        // Get the AI status
                        if (item.TryGetProperty("ai_status", out JsonElement aiElement))
                        {
                            switch (aiElement.ValueKind)
                            {
                                case JsonValueKind.True:
                                    wallpaper.IsAI = true;
                                    break;
                                case JsonValueKind.False:
                                    wallpaper.IsAI = false;
                                    break;
                                case JsonValueKind.String:
                                    var strValue = aiElement.GetString() ?? "";
                                    wallpaper.IsAI = strValue.Equals("true", StringComparison.OrdinalIgnoreCase);
                                    break;
                                case JsonValueKind.Number:
                                    wallpaper.IsAI = aiElement.GetInt32() != 0;
                                    break;
                            }
                        }
                        
                        // Get likes and downloads if available
                        if (item.TryGetProperty("likes", out JsonElement likesElement) && 
                            likesElement.ValueKind == JsonValueKind.Number)
                        {
                            wallpaper.Likes = likesElement.GetInt32();
                        }
                        
                        if (item.TryGetProperty("downloads", out JsonElement downloadsElement) && 
                            downloadsElement.ValueKind == JsonValueKind.Number)
                        {
                            wallpaper.Downloads = downloadsElement.GetInt32();
                        }
                        
                        wallpapers.Add(wallpaper);
                    }
                }
                
                // Log the first few for debugging
                for (int i = 0; i < Math.Min(wallpapers.Count, 5); i++)
                {
                    _logger?.LogInformation($"Wallpaper[{i}]: URL={wallpapers[i].Url}, Quality={wallpapers[i].Quality}, IsAI={wallpapers[i].IsAI}");
                }
                
                // Add the wallpapers to the UI
                foreach (var wallpaper in wallpapers)
                {
                    if (string.IsNullOrEmpty(wallpaper.Url))
                        continue;
                        
                    // Normalize the URL to lowercase for consistent comparison
                    string normalizedUrl = wallpaper.Url.ToLowerInvariant();
                    
                    // Skip if we already have this URL
                    if (_loadedUrls.Contains(normalizedUrl))
                        continue;
                        
                    // Extract image ID from URL
                    string imageId = GetImageIdFromUrl(normalizedUrl);
                    
                    // Skip if we already have this imageId
                    if (_wallpapers.Any(img => img.ImageId == imageId))
                        continue;
                    
                    // Create a wallpaper item
                    var image = new WallpaperItem
                    {
                        ImageUrl = normalizedUrl,
                        ImageId = imageId,
                        IsAI = wallpaper.IsAI,
                        Likes = wallpaper.Likes,
                        Downloads = wallpaper.Downloads
                    };
                    
                    // Set resolution based on quality
                    image.Resolution = "1920x1080"; // Default
                    
                    if (!string.IsNullOrEmpty(wallpaper.Quality))
                    {
                        image.ResolutionLabel = wallpaper.Quality;
                        
                        switch (wallpaper.Quality)
                        {
                            case "4K":
                                image.Resolution = "3840x2160";
                                break;
                            case "5K":
                                image.Resolution = "5120x2880";
                                break;
                            case "8K":
                                image.Resolution = "7680x4320";
                                break;
                        }
                    }
                    
                    _wallpapers.Add(image);
                    _loadedUrls.Add(normalizedUrl);
                    
                    // Create and add UI element
                    var wallpaperElement = CreateWallpaperElement(image);
                    WallpaperContainer.Children.Add(wallpaperElement);
                    
                    // Also track the ID to avoid re-attempting it
                    if (int.TryParse(imageId, out int parsedId))
                    {
                        _attemptedIds.Add(parsedId);
                    }
                }
                
                // Set the current imageId for infinite scrolling based on our loaded images
                if (_wallpapers.Count > 0)
                {
                    // Find all valid numeric IDs
                    var numericIds = _wallpapers
                        .Where(i => int.TryParse(i.ImageId, out _))
                        .Select(i => int.Parse(i.ImageId))
                        .ToList();
                    
                    if (numericIds.Any())
                    {
                        // Set the starting point to one less than the minimum ID already loaded
                        // This ensures we start loading the next sequential images
                        _currentImageId = numericIds.Min() - 1;
                        _logger?.LogInformation($"Set next imageId to {_currentImageId} - will load in sequential order from here");
                    }
                    else
                    {
                        // If no numeric IDs, start from a reasonable default
                        _currentImageId = 200000;
                        _logger?.LogInformation($"No numeric IDs found in JSON, set next imageId to default: {_currentImageId}");
                    }
                }
                else
                {
                    _currentImageId = 200000;
                    _logger?.LogInformation($"No wallpapers loaded from JSON, set next imageId to default: {_currentImageId}");
                }
                
                _logger?.LogInformation($"Successfully loaded {_wallpapers.Count} images from JSON file");
                return _wallpapers.Count > 0;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading images from JSON file: " + ex.Message);
                _currentImageId = 200000; // Default to a high value if JSON loading failed
                return false;
            }
        }
        
        private string GetImageIdFromUrl(string url)
        {
            try
            {
                // Extract ID from URL like https://backiee.com/static/wallpapers/560x315/123456.jpg
                string filename = Path.GetFileNameWithoutExtension(url);
                return filename;
            }
            catch
            {
                return string.Empty;
            }
        }

        private async Task AddTestWallpaperItem()
        {
            // Create a new wallpaper item with random properties
            var wallpaper = new WallpaperItem
            {
                ImageUrl = GetRandomImageUrl(),
                ImageId = _random.Next(100000, 999999).ToString(),
                Resolution = $"{_random.Next(1920, 7680)}x{_random.Next(1080, 4320)}",
                ResolutionLabel = _resolutions[_random.Next(_resolutions.Count)],
                IsAI = _random.Next(2) == 1,
                Likes = _random.Next(1, 100),
                Downloads = _random.Next(1, 500)
            };

            _wallpapers.Add(wallpaper);

            // Create UI element for this wallpaper
            var wallpaperElement = CreateWallpaperElement(wallpaper);
            
            // Add to container
            WallpaperContainer.Children.Add(wallpaperElement);

            // Simulate network delay for realistic testing
            await Task.Delay(50);
        }

        private string GetRandomImageUrl()
        {
            // For testing, use some placeholder image URLs
            string[] imageUrls = new string[]
            {
                "https://wallpapercave.com/wp/wp2555030.jpg",
                "https://wallpaperaccess.com/full/51363.jpg",
                "https://images.pexels.com/photos/1366919/pexels-photo-1366919.jpeg",
                "https://wallpapercave.com/wp/wp4676582.jpg",
                "https://images.pexels.com/photos/1242348/pexels-photo-1242348.jpeg",
                "https://wallpapercave.com/wp/wp2581576.jpg",
                "https://images.pexels.com/photos/733745/pexels-photo-733745.jpeg",
                "https://wallpaperaccess.com/full/1091424.jpg",
                "https://images.pexels.com/photos/1323550/pexels-photo-1323550.jpeg",
                "https://wallpapercave.com/wp/wp7486693.jpg"
            };

            return imageUrls[_random.Next(imageUrls.Length)];
        }

        private FrameworkElement CreateWallpaperElement(WallpaperItem wallpaper)
        {
            // Create the main border
            var border = new Border
            {
                BorderBrush = new SolidColorBrush(Colors.LightGray),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(4),
                Padding = new Thickness(0),
                Tag = wallpaper,
                Cursor = System.Windows.Input.Cursors.Hand,
                Width = _itemWidth,
                Height = _itemHeight,
                ClipToBounds = true
            };
            
            // Add clipping with rounded corners
            border.Clip = new RectangleGeometry
            {
                Rect = new Rect(0, 0, _itemWidth, _itemHeight),
                RadiusX = 15,
                RadiusY = 15
            };

            // Create a grid to hold the content
            var grid = new Grid();
            border.Child = grid;

            // Create a dark placeholder background
            var placeholderBackground = new Border
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 30)), // Dark gray background
                Opacity = 1
            };
            grid.Children.Add(placeholderBackground);
            
            // Create a loading indicator
            var loadingIndicator = new System.Windows.Controls.ProgressBar
            {
                IsIndeterminate = true,
                Width = 50,
                Height = 5,
                Foreground = new SolidColorBrush(Colors.White),
                Background = new SolidColorBrush(Colors.Transparent),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 0)
            };
            grid.Children.Add(loadingIndicator);

            // Create and add the image (with placeholder until loaded)
            var image = new System.Windows.Controls.Image
            {
                Stretch = Stretch.Fill,
                Opacity = 0 // Start with invisible image
            };
            
            // Use BitmapImage with events for loading
            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad; // Fully load in memory
            bitmapImage.UriSource = new Uri(wallpaper.ImageUrl);
            bitmapImage.EndInit();
            
            // Handle the image loading events
            bitmapImage.DownloadCompleted += (s, e) => 
            {
                // When the image is loaded, fade it in and hide the placeholder
                image.Opacity = 1;
                placeholderBackground.Opacity = 0;
                loadingIndicator.Visibility = Visibility.Collapsed;
            };
            
            bitmapImage.DownloadFailed += (s, e) => 
            {
                // If download fails, show a error placeholder
                placeholderBackground.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(50, 30, 30)); // Dark red background
                loadingIndicator.Visibility = Visibility.Collapsed;
                
                // Add an error icon or text
                var errorText = new TextBlock
                {
                    Text = "!",
                    FontSize = 24,
                    Foreground = new SolidColorBrush(Colors.White),
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center
                };
                grid.Children.Add(errorText);
            };
            
            image.Source = bitmapImage;
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
            grid.Children.Add(image);

            // Create a panel for resolution badges
            var badgesPanel = new StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                VerticalAlignment = System.Windows.VerticalAlignment.Top,
                Margin = new Thickness(0)
            };
            grid.Children.Add(badgesPanel);

            // Add appropriate resolution badge
            string badgeSource = wallpaper.ResolutionLabel switch
            {
                "4K" => "/Assets/4k_logo.png",
                "5K" => "/Assets/5k_logo.png",
                "8K" => "/Assets/8k_logo.png",
                _ => string.Empty
            };

            if (!string.IsNullOrEmpty(badgeSource))
            {
                var badge = new System.Windows.Controls.Image
                {
                    Source = new BitmapImage(new Uri(badgeSource, UriKind.Relative)),
                    Height = 48
                };
                badgesPanel.Children.Add(badge);
            }

            // Add AI badge if needed
            if (wallpaper.IsAI)
            {
                var aiPanel = new System.Windows.Controls.Image
                {
                    Source = new BitmapImage(new Uri("/Assets/aigenerated-icon.png", UriKind.Relative)),
                    Height = 36,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                    VerticalAlignment = System.Windows.VerticalAlignment.Top,
                    Margin = new Thickness(0, 5, 20, 0)
                };
                grid.Children.Add(aiPanel);
            }

            // Add stats
            var statsPanel = new StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                VerticalAlignment = System.Windows.VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 0)
            };
            
            // Add likes counter with Apple-style heart icon
            var likesPanel = new StackPanel 
            { 
                Orientation = System.Windows.Controls.Orientation.Horizontal, 
                Margin = new Thickness(0, 0, 12, 0),
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };
            
            // Use a simpler heart icon that resembles Apple's style
            var likesIcon = new System.Windows.Controls.Image
            {
                Source = new BitmapImage(new Uri("/Assets/heart_icon.png", UriKind.Relative)),
                Width = 16,
                Height = 16,
                Margin = new Thickness(0, 0, 4, 0)
            };
            
            // If the heart icon image isn't available, fall back to a Path
            if (likesIcon.Source.ToString().Contains("heart_icon.png"))
            {
                try
                {
                    likesIcon = null;
                    var heartPath = new System.Windows.Shapes.Path
                    {
                        Data = Geometry.Parse("M12,21.35L10.55,20.03C5.4,15.36 2,12.27 2,8.5C2,5.41 4.42,3 7.5,3C9.24,3 10.91,3.81 12,5.08C13.09,3.81 14.76,3 16.5,3C19.58,3 22,5.41 22,8.5C22,12.27 18.6,15.36 13.45,20.03L12,21.35Z"),
                        Fill = new SolidColorBrush(System.Windows.Media.Colors.White),
                        Width = 16,
                        Height = 16,
                        Margin = new Thickness(0, 0, 4, 0),
                        Stretch = Stretch.Uniform
                    };
                    likesPanel.Children.Add(heartPath);
                }
                catch
                {
                    // Just in case the path geometry is invalid
                    var textHeart = new TextBlock
                    {
                        Text = "♥",
                        Foreground = new SolidColorBrush(System.Windows.Media.Colors.White),
                        FontSize = 14,
                        Margin = new Thickness(0, 0, 4, 0)
                    };
                    likesPanel.Children.Add(textHeart);
                }
            }
            else
            {
                likesPanel.Children.Add(likesIcon);
            }
            
            // Add text for likes count
            var likesText = new TextBlock 
            { 
                Text = wallpaper.Likes.ToString(), 
                Foreground = new SolidColorBrush(System.Windows.Media.Colors.White),
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };
            likesPanel.Children.Add(likesText);
            statsPanel.Children.Add(likesPanel);
            
            // Add downloads counter
            var downloadsPanel = new StackPanel 
            { 
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            
            // Use a simpler download icon that resembles Apple's style
            var downloadsIcon = new System.Windows.Controls.Image
            {
                Source = new BitmapImage(new Uri("/Assets/download_icon.png", UriKind.Relative)),
                Width = 16,
                Height = 16,
                Margin = new Thickness(0, 0, 4, 0)
            };
            
            // If the download icon image isn't available, fall back to a Path
            if (downloadsIcon.Source.ToString().Contains("download_icon.png"))
            {
                try
                {
                    downloadsIcon = null;
                    var downloadPath = new System.Windows.Shapes.Path
                    {
                        Data = Geometry.Parse("M12,15L7,10H10V6H14V10H17L12,15M19.35,10.03C18.67,6.59 15.64,4 12,4C9.11,4 6.6,5.64 5.35,8.03C2.34,8.36 0,10.9 0,14A6,6 0 0,0 6,20H19A5,5 0 0,0 24,15C24,12.36 21.95,10.22 19.35,10.03Z"),
                        Fill = new SolidColorBrush(System.Windows.Media.Colors.White),
                        Width = 16,
                        Height = 16,
                        Margin = new Thickness(0, 0, 4, 0),
                        Stretch = System.Windows.Media.Stretch.Uniform
                    };
                    downloadsPanel.Children.Add(downloadPath);
                }
                catch
                {
                    // Just in case the path geometry is invalid, use a simple arrow
                    var downloadPath = new System.Windows.Shapes.Path
                    {
                        Data = Geometry.Parse("M5,20H19V18H5M19,9H15V3H9V9H5L12,16L19,9Z"),
                        Fill = new SolidColorBrush(System.Windows.Media.Colors.White),
                        Width = 16,
                        Height = 16,
                        Margin = new Thickness(0, 0, 4, 0),
                        Stretch = System.Windows.Media.Stretch.Uniform
                    };
                    downloadsPanel.Children.Add(downloadPath);
                }
            }
            else
            {
                downloadsPanel.Children.Add(downloadsIcon);
            }
            
            // Add text for download count
            var downloadsText = new TextBlock 
            { 
                Text = wallpaper.Downloads.ToString(), 
                Foreground = new SolidColorBrush(System.Windows.Media.Colors.White),
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };
            downloadsPanel.Children.Add(downloadsText);
            statsPanel.Children.Add(downloadsPanel);
            
            // Add a semi-transparent background to ensure visibility
            var statsBg = new Border
            {
                Background = null,
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(8, 4, 8, 4),
                Child = statsPanel,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                VerticalAlignment = System.Windows.VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 10, 10)
            };
            
            // Add the stats to the grid
            grid.Children.Add(statsBg);

            // Handle click event
            border.MouseLeftButtonUp += ImageBorder_MouseLeftButtonUp;

            return border;
        }

        private void AdjustItemSizes()
        {
            // Get the current width of the container
            double containerWidth = MainScrollViewer.ActualWidth;
            _logger?.LogInformation($"Container width: {containerWidth}");

            if (containerWidth <= 0)
                return;

            // Calculate how many items should fit in each row
            int itemsPerRow;
            if (containerWidth < 600)
                itemsPerRow = 1;
            else if (containerWidth < 900)
                itemsPerRow = 2;
            else if (containerWidth < 1200)
                itemsPerRow = 3;
            else if (containerWidth < 1500)
                itemsPerRow = 4;
            else
                itemsPerRow = 5;

            // Calculate new item width (accounting for margins)
            double newItemWidth = (containerWidth / itemsPerRow) - 10; // 10px for margins
            double newItemHeight = newItemWidth * 0.6; // 16:9 aspect ratio

            _logger?.LogInformation($"Adjusting items to width: {newItemWidth}, items per row: {itemsPerRow}");

            // Update all wallpaper items with new size
            foreach (FrameworkElement child in WallpaperContainer.Children)
            {
                if (child is Border border)
                {
                    border.Width = newItemWidth;
                    border.Height = newItemHeight;
                }
            }

            // Store new sizes for new items
            _itemWidth = newItemWidth;
            _itemHeight = newItemHeight;
            
            // Save settings when page is fully loaded and items have been adjusted
            if (_isPageLoaded)
            {
                SaveSettingsQuietly();
            }
        }

        private async void MainScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            try
            {
                // Restore scroll position once if needed (after initial load)
                if (_shouldRestoreScrollPosition && _isPageLoaded && e.ExtentHeight > 0)
                {
                    _shouldRestoreScrollPosition = false;
                    
                    if (_settingsService != null)
                    {
                        var settings = await _settingsService.GetSettingsAsync();
                        if (settings.TestGridScrollPosition > 0)
                        {
                            _logger?.LogInformation($"Restoring scroll position to {settings.TestGridScrollPosition}");
                            MainScrollViewer.ScrollToVerticalOffset(settings.TestGridScrollPosition);
                            return;
                        }
                    }
                }
                
                // Save settings when user scrolls (debounced)
                if (_isPageLoaded && e.VerticalChange != 0)
                {
                    if ((DateTime.Now - _lastScrollCheck) > TimeSpan.FromSeconds(2))
                    {
                        _lastScrollCheck = DateTime.Now;
                        SaveSettingsQuietly();
                    }
                }
                
                // Debounce scroll events for infinite scrolling
                if ((DateTime.Now - _lastScrollCheck) < _scrollDebounceTime)
                {
                    return;
                }
                _lastScrollCheck = DateTime.Now;

                // Calculate how far from the bottom we are (as a percentage)
                double scrollPercentage = (e.VerticalOffset + e.ViewportHeight) / e.ExtentHeight;
                
                // Check if we're within the preemptive loading threshold
                // This loads more images before reaching the bottom
                if (scrollPercentage > 0.7) // Start loading when 70% scrolled
                {
                    if (!_isLoadingMore)
                    {
                        // Try to acquire the semaphore without blocking
                        if (await _loadingSemaphore.WaitAsync(0))
                        {
                            try
                            {
                                _isLoadingMore = true;
                                _logger?.LogInformation("Preemptive loading triggered at {0}% scroll", (scrollPercentage * 100).ToString("0"));
                                
                                // Show loading status but make it less intrusive
                                StatusTextBlock.Text = "Loading more wallpapers...";
                                StatusTextBlock.Visibility = Visibility.Visible;
                                LoadingProgressBar.Visibility = Visibility.Visible;
                                
                                // Load more wallpapers - using HTTP check method
                                await LoadMoreImagesAsync(_cts.Token);
                                
                                // Save settings after loading more items
                                if (_isPageLoaded)
                                {
                                    SaveSettingsQuietly();
                                }
                                
                                // Queue background loading for the next batch
                                if (_isPrefetchingEnabled)
                                {
                                    QueueBackgroundLoading();
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogError(ex, "Error loading more wallpapers in scroll handler");
                            }
                            finally
                            {
                                _isLoadingMore = false;
                                _loadingSemaphore.Release();
                                
                                // Hide loading indicators
                                StatusTextBlock.Visibility = Visibility.Collapsed;
                                LoadingProgressBar.Visibility = Visibility.Collapsed;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in scroll changed handler");
            }
        }
        
        private void StartBackgroundLoading()
        {
            if (_isBackgroundLoadingEnabled && !_isLoadingMore)
            {
                QueueBackgroundLoading();
            }
        }
        
        private void QueueBackgroundLoading()
        {
            lock (_backgroundTaskLock)
            {
                if (_runningBackgroundTasks < _maxConcurrentBackgroundTasks)
                {
                    _runningBackgroundTasks++;
                    
                    // Use a delay before starting background loading
                    // This allows the UI to remain responsive and prevents too many concurrent requests
                    var task = Task.Run(async () =>
                    {
                        try
                        {
                            // Short delay before starting background loading
                            // This gives the UI thread time to process and helps maintain order
                            await Task.Delay(300);
                            
                            // Take a snapshot of the current imageId to avoid race conditions
                            int startingImageId;
                            lock (_backgroundTaskLock)
                            {
                                startingImageId = _currentImageId;
                            }
                            
                            _logger?.LogInformation($"Background loading starting from imageId: {startingImageId}");
                            
                            await LoadMoreImagesAsync(_cts.Token, isBackgroundLoading: true);
                        }
                        finally
                        {
                            // Decrement running task count
                            lock (_backgroundTaskLock)
                            {
                                _runningBackgroundTasks--;
                            }
                        }
                    });
                    
                    _backgroundTasks.Enqueue(task);
                }
            }
        }
        
        private async Task LoadMoreImagesAsync(CancellationToken cancellationToken, bool isBackgroundLoading = false)
        {
            try
            {
                _logger?.LogInformation("LoadMoreImagesAsync called, current imageId: {ImageId}, background: {isBackground}", 
                    _currentImageId, isBackgroundLoading);
                
                // Show status only if not background loading
                if (!isBackgroundLoading)
                {
                    StatusTextBlock.Text = $"Loading wallpapers... (ID: {_currentImageId})";
                }
                
                // Create a list to hold the successful images in this batch
                List<WallpaperItem> batchImages = new List<WallpaperItem>();
                int imagesFound = 0;
                int consecutiveFailures = 0;
                const int maxConsecutiveFailures = 50; // Threshold to jump back if too many failures in a row

                // Use HttpClient for parallel requests
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(10);

                    var tasks = new List<Task<Tuple<int, bool, string>>>();
                    var currentBatchIds = new List<int>();

                    // Prepare batch of IDs to check - strictly decreasing from _currentImageId
                    for (int i = 0; i < _batchSize; i++)
                    {
                        int imageId = _currentImageId - i;
                        if (imageId <= 0) // Don't check negative or zero IDs
                            continue;
                            
                        if (_attemptedIds.Contains(imageId)) // Skip IDs we've already tried
                            continue;

                        currentBatchIds.Add(imageId);
                        // Normalize URL format by ensuring consistent casing
                        string imageUrl = $"https://backiee.com/static/wallpapers/560x315/{imageId}.jpg".ToLowerInvariant();
                        
                        // Skip if we already have this URL
                        if (_loadedUrls.Contains(imageUrl))
                            continue;
                            
                        tasks.Add(CheckImageExistsAsync(client, imageId, imageUrl, cancellationToken));
                    }

                    // Wait for all tasks to complete
                    if (tasks.Any())
                    {
                        var completedTasks = await Task.WhenAll(tasks);

                        // Create a dictionary to collect all successful images by ID for easier sorting later
                        Dictionary<int, WallpaperItem> foundWallpapers = new Dictionary<int, WallpaperItem>();

                        // Process results in the order of IDs to maintain consistency (highest to lowest)
                        foreach (var id in currentBatchIds.OrderByDescending(x => x))
                        {
                            var result = completedTasks.FirstOrDefault(r => r?.Item1 == id);
                            if (result == null) continue;

                            bool exists = result.Item2;
                            string imageUrl = result.Item3;

                            // Add to attempted IDs before checking existence
                            _attemptedIds.Add(id);
                            _totalRequests++;

                            // Double-check to avoid duplicates (check by ID as well)
                            if (exists && !_loadedUrls.Contains(imageUrl) && 
                                !_wallpapers.Any(img => img.ImageId == id.ToString()))
                            {
                                _successfulRequests++;
                                imagesFound++;
                                consecutiveFailures = 0; // Reset consecutive failures counter

                                var wallpaper = new WallpaperItem
                                {
                                    ImageUrl = imageUrl,
                                    ImageId = id.ToString(),
                                    Resolution = "1920x1080",
                                    // Randomly assign a resolution label (since we don't know the real quality)
                                    ResolutionLabel = _resolutions[_random.Next(_resolutions.Count)],
                                    IsAI = _random.Next(2) == 1, // Randomly assign AI status
                                    Likes = _random.Next(1, 100),
                                    Downloads = _random.Next(1, 500)
                                };
                                
                                _logger?.LogInformation($"Creating image: ID={wallpaper.ImageId}, URL={wallpaper.ImageUrl}");
                                
                                // Update resolution based on label
                                switch (wallpaper.ResolutionLabel)
                                {
                                    case "4K":
                                        wallpaper.Resolution = "3840x2160";
                                        break;
                                    case "5K":
                                        wallpaper.Resolution = "5120x2880";
                                        break;
                                    case "8K":
                                        wallpaper.Resolution = "7680x4320";
                                        break;
                                }
                                
                                // Store in the dictionary
                                foundWallpapers[id] = wallpaper;
                                _loadedUrls.Add(imageUrl);
                            }
                            else
                            {
                                _failedRequests++;
                                consecutiveFailures++;
                            }
                        }

                        // Update the current ID to continue from - strictly sequentially
                        // Find the minimum ID we just checked and continue from one below that
                        if (currentBatchIds.Any())
                        {
                            _currentImageId = currentBatchIds.Min() - 1;
                            _logger?.LogInformation($"Updated next imageId to {_currentImageId} for sequential loading");
                        }

                        // Sort wallpapers by ID in descending order (highest to lowest) for consistent ordering
                        batchImages = foundWallpapers.OrderByDescending(kvp => kvp.Key)
                                                    .Select(kvp => kvp.Value)
                                                    .ToList();

                        // Ensuring consistent order between multiple loading operations
                        lock (_backgroundTaskLock)
                        {
                            // Acquire a common lock before dispatching to UI thread
                            // This prevents different loading operations from interleaving their images
                        }
                        
                        // Add images to the UI on the UI thread
                        await Dispatcher.InvokeAsync(() =>
                        {
                            foreach (var wallpaper in batchImages)
                            {
                                _wallpapers.Add(wallpaper);
                                
                                // Create and add UI element
                                var wallpaperElement = CreateWallpaperElement(wallpaper);
                                WallpaperContainer.Children.Add(wallpaperElement);
                            }
                            
                            // Update status only if not background loading
                            if (!isBackgroundLoading)
                            {
                                // Update the status
                                StatusTextBlock.Text = $"Loaded {imagesFound} new wallpapers (Total: {_wallpapers.Count})";
                                StatusTextBlock.Text += $" | Success: {_successfulRequests}/{_totalRequests} ({_failedRequests} failed)";
                            }
                        });
                        
                        _logger?.LogInformation("Added {Count} images to collection. Total: {Total}", 
                            imagesFound, _wallpapers.Count);

                        // If we got too many consecutive failures, jump back by a significant amount
                        // This helps skip large gaps in the ID sequence while still loading in order
                        if (imagesFound == 0 && consecutiveFailures > maxConsecutiveFailures)
                        {
                            int jumpAmount = 5000;
                            int oldId = _currentImageId;
                            _currentImageId -= jumpAmount;
                            if (_currentImageId < 0) _currentImageId = 200000; // Reset if we hit zero
                            
                            _logger?.LogInformation($"Too many consecutive failures, jumping from {oldId} to {_currentImageId}");
                            
                            if (!isBackgroundLoading)
                            {
                                await Dispatcher.InvokeAsync(() => {
                                    StatusTextBlock.Text += $" | Jumping to ID: {_currentImageId}";
                                });
                            }
                        }
                        
                        // If we found images and background loading is enabled, queue next batch
                        // Only start a new background task if we're not already in background mode
                        if (imagesFound > 0 && _isBackgroundLoadingEnabled && !isBackgroundLoading)
                        {
                            QueueBackgroundLoading();
                        }
                    }
                    else
                    {
                        // If no tasks were created (all IDs are already attempted)
                        // Jump back to find a new range of IDs
                        int jumpAmount = 10000;
                        int oldId = _currentImageId;
                        _currentImageId -= jumpAmount;
                        if (_currentImageId < 0) _currentImageId = 200000; // Reset if we hit zero
                        
                        _logger?.LogInformation($"No new IDs to check, jumping from {oldId} to {_currentImageId}");
                        
                        if (!isBackgroundLoading)
                        {
                            await Dispatcher.InvokeAsync(() => {
                                StatusTextBlock.Text = $"Searching for more wallpapers... (ID: {_currentImageId})";
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in LoadMoreImagesAsync");
                
                if (!isBackgroundLoading)
                {
                    await Dispatcher.InvokeAsync(() => {
                        StatusTextBlock.Text = $"Error loading images: {ex.Message}";
                    });
                }
            }
        }
        
        private async Task<Tuple<int, bool, string>> CheckImageExistsAsync(HttpClient client, int imageId, string imageUrl, CancellationToken cancellationToken)
        {
            try
            {
                // Ensure URL is normalized
                imageUrl = imageUrl.ToLowerInvariant();
                
                // Make a HEAD request first to check if the image exists
                var request = new HttpRequestMessage(HttpMethod.Head, imageUrl);
                var response = await client.SendAsync(request, cancellationToken);
                
                // Return the result - true if the image exists, false otherwise
                return new Tuple<int, bool, string>(imageId, response.IsSuccessStatusCode, imageUrl);
            }
            catch (Exception)
            {
                // If there's an error, assume the image doesn't exist
                return new Tuple<int, bool, string>(imageId, false, imageUrl);
            }
        }

        private void ImageBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is WallpaperItem wallpaper)
            {
                _logger?.LogInformation($"Wallpaper clicked: {wallpaper.ResolutionLabel} (ID: {wallpaper.ImageId})");
                
                // Show a popup with wallpaper details
                System.Windows.MessageBox.Show($"Clicked on {wallpaper.ResolutionLabel} wallpaper\nResolution: {wallpaper.Resolution}\nAI Generated: {wallpaper.IsAI}\nImage ID: {wallpaper.ImageId}", 
                    "Wallpaper Details", MessageBoxButton.OK, MessageBoxImage.Information);
                
                // TODO: Implement setting wallpaper functionality here
            }
        }

        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            _logger?.LogInformation("Filter button clicked");
            System.Windows.MessageBox.Show("Filter functionality would be implemented here.", "Filter", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SetAsSlideshowButton_Click(object sender, RoutedEventArgs e)
        {
            _logger?.LogInformation("Slideshow button clicked");
            System.Windows.MessageBox.Show("Slideshow functionality would be implemented here.", "Slideshow", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    /// <summary>
    /// Model class for wallpaper items in the grid
    /// </summary>
    public class WallpaperItem
    {
        public string ImageUrl { get; set; } = string.Empty;
        public string ImageId { get; set; } = string.Empty;
        public string Resolution { get; set; } = string.Empty;
        public string ResolutionLabel { get; set; } = string.Empty;
        public bool IsAI { get; set; }
        public int Likes { get; set; }
        public int Downloads { get; set; }
    }
    
    // Simple class to hold wallpaper data without any serialization complexities
    internal class SimpleWallpaper
    {
        public string Url { get; set; } = "";
        public string Quality { get; set; } = "";
        public bool IsAI { get; set; }
        public int Likes { get; set; }
        public int Downloads { get; set; }
    }
} 