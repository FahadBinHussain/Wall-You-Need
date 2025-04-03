using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Text.Json;
using WallYouNeed.Core.Models;

namespace WallYouNeed.App.Pages
{
    /// <summary>
    /// Interaction logic for BackieeImagesPage.xaml
    /// </summary>
    public partial class BackieeImagesPage : Page
    {
        private ObservableCollection<BackieeImage> Images { get; set; }
        private ILogger<BackieeImagesPage> _logger;

        // Infinite scrolling variables
        private int _currentImageId = 418183; // Start with the highest ID from the sample
        private volatile bool _isLoadingMore = false;
        private readonly SemaphoreSlim _loadingSemaphore = new SemaphoreSlim(1, 1);
        private readonly int _batchSize = 20; // Number of images to load at once
        private readonly int _scrollThreshold = 400; // Increased threshold for smoother loading
        private CancellationTokenSource _cts;
        private DateTime _lastScrollCheck = DateTime.MinValue;
        private readonly TimeSpan _scrollDebounceTime = TimeSpan.FromMilliseconds(250);
        
        // Cache to track which IDs have been attempted and loaded
        private HashSet<int> _attemptedIds = new HashSet<int>();
        private HashSet<string> _loadedUrls = new HashSet<string>();
        
        // Stats counters
        private int _totalRequests = 0;
        private int _successfulRequests = 0;
        private int _failedRequests = 0;

        public BackieeImagesPage(ILogger<BackieeImagesPage> logger = null)
        {
            try
            {
                _logger = logger;
                _logger?.LogInformation("BackieeImagesPage constructor called");
                
                InitializeComponent();
                
                // The BooleanToVisibilityConverter is already defined in XAML
                
                // If it wasn't already defined in XAML, add it here
                if (!Resources.Contains("BooleanToVisibilityConverter"))
                {
                    Resources.Add("BooleanToVisibilityConverter", new BooleanToVisibilityConverter());
                }
                
                Images = new ObservableCollection<BackieeImage>();
                ImagesItemsControl.ItemsSource = Images;
                
                // Load images asynchronously
                Loaded += BackieeImagesPage_Loaded;
                
                // Create a new cancellation token source for infinite scrolling
                _cts = new CancellationTokenSource();
                
                _logger?.LogInformation("BackieeImagesPage initialized successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error initializing BackieeImagesPage");
                System.Windows.MessageBox.Show($"Error initializing BackieeImagesPage: {ex.Message}", 
                    "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BackieeImagesPage_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _logger?.LogInformation("BackieeImagesPage_Loaded event fired");
                
                // Show loading indicators
                StatusTextBlock.Visibility = Visibility.Visible;
                LoadingProgressBar.Visibility = Visibility.Visible;
                
                // Load initial images from JSON
                await FetchInitialImages();
                
                // Hide loading indicators
                StatusTextBlock.Visibility = Visibility.Collapsed;
                LoadingProgressBar.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in BackieeImagesPage_Loaded event handler");
                System.Windows.MessageBox.Show($"Error loading BackieeImagesPage: {ex.Message}", 
                    "Loading Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task FetchInitialImages()
        {
            try
            {
                _logger?.LogInformation("FetchInitialImages called");
                
                // Show loading indicator or message
                System.Windows.Application.Current.Dispatcher.Invoke(() => {
                    Images.Clear();
                    _loadedUrls.Clear();
                    StatusTextBlock.Text = "Loading initial wallpapers...";
                    StatusTextBlock.Visibility = Visibility.Visible;
                    LoadingProgressBar.Visibility = Visibility.Visible;
                });
                
                // Directly load from JSON instead of web scraping
                await LoadImagesFromJsonFile();
                
                _logger?.LogInformation("Successfully added {Count} initial images to the collection", Images.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in FetchInitialImages");
                System.Windows.MessageBox.Show($"Error fetching initial images: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void MainScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            try
            {
                // Debounce scroll events
                if ((DateTime.Now - _lastScrollCheck) < _scrollDebounceTime)
                {
                    return;
                }
                _lastScrollCheck = DateTime.Now;

                // Check if we're near the bottom of the scroll viewer
                if (e.VerticalOffset + e.ViewportHeight + _scrollThreshold >= e.ExtentHeight)
                {
                    // Try to acquire the semaphore without blocking
                    if (!_isLoadingMore && await _loadingSemaphore.WaitAsync(0))
                    {
                        try
                        {
                            _isLoadingMore = true;
                            await LoadMoreImagesAsync(_cts.Token);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Error loading more images in scroll handler");
                        }
                        finally
                        {
                            _isLoadingMore = false;
                            _loadingSemaphore.Release();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in scroll changed handler");
            }
        }

        private async Task LoadMoreImagesAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger?.LogInformation("LoadMoreImagesAsync called, current imageId: {ImageId}", _currentImageId);
                
                // Show status to indicate loading is in progress
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => {
                    StatusTextBlock.Text = $"Loading wallpapers... (ID: {_currentImageId})";
                    StatusTextBlock.Visibility = Visibility.Visible;
                    LoadingProgressBar.Visibility = Visibility.Visible;
                });

                // Create a list to hold the successful images in this batch
                List<BackieeImage> batchImages = new List<BackieeImage>();
                int consecutiveMisses = 0;
                int imagesFound = 0;

                // Use HttpClient for parallel requests
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(10);

                    var tasks = new List<Task<Tuple<int, bool, string>>>();
                    var currentBatchIds = new List<int>();

                    // Prepare batch of IDs to check
                    for (int i = 0; i < _batchSize; i++)
                    {
                        int imageId = _currentImageId - i;
                        if (imageId <= 0 || _attemptedIds.Contains(imageId))
                            continue;

                        currentBatchIds.Add(imageId);
                        string imageUrl = $"https://backiee.com/static/wallpapers/560x315/{imageId}.jpg";
                        
                        // Skip if we already have this URL
                        if (_loadedUrls.Contains(imageUrl))
                            continue;
                            
                        tasks.Add(CheckImageExistsAsync(client, imageId, imageUrl, cancellationToken));
                    }

                    // Wait for all tasks to complete with a timeout
                    if (tasks.Any())
                    {
                        var completedTasks = await Task.WhenAll(tasks);

                        // Process results in the order of IDs to maintain consistency
                        foreach (var id in currentBatchIds)
                        {
                            var result = completedTasks.FirstOrDefault(r => r?.Item1 == id);
                            if (result == null) continue;

                            bool exists = result.Item2;
                            string imageUrl = result.Item3;

                            // Add to attempted IDs before checking existence
                            _attemptedIds.Add(id);
                            _totalRequests++;

                            if (exists && !_loadedUrls.Contains(imageUrl))
                            {
                                _successfulRequests++;
                                imagesFound++;
                                consecutiveMisses = 0;

                                var image = new BackieeImage
                                {
                                    ImageUrl = imageUrl,
                                    ImageId = id.ToString(),
                                    IsLoading = false,
                                    Resolution = "1920x1080"
                                };
                                
                                // Set resolution properties to display tags
                                image.SetResolutionProperties();
                                
                                // Debug verification
                                _logger?.LogInformation($"Creating image: ID={image.ImageId}, URL={image.ImageUrl}");
                                
                                batchImages.Add(image);
                                
                                _loadedUrls.Add(imageUrl);
                            }
                            else
                            {
                                _failedRequests++;
                                consecutiveMisses++;
                            }
                        }

                        // Update the current ID to continue from
                        if (currentBatchIds.Any())
                        {
                            _currentImageId = currentBatchIds.Min() - 1;
                        }

                        // Add images in the order they were discovered
                        foreach (var image in batchImages)
                        {
                            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                Images.Add(image);
                            });
                        }

                        // Update the status
                        StatusTextBlock.Text = $"Loaded {imagesFound} new wallpapers (Total: {Images.Count})";
                        _logger?.LogInformation("Added {Count} images to collection. Total: {Total}", 
                            imagesFound, Images.Count);

                        StatusTextBlock.Text += $" | Success: {_successfulRequests}/{_totalRequests} ({_failedRequests} failed)";
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in LoadMoreImagesAsync");
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => {
                    StatusTextBlock.Text = $"Error loading images: {ex.Message}";
                });
            }
            finally
            {
                // Hide loading indicators
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => {
                    StatusTextBlock.Visibility = Visibility.Collapsed;
                    LoadingProgressBar.Visibility = Visibility.Collapsed;
                });
            }
        }
        
        private async Task<Tuple<int, bool, string>> CheckImageExistsAsync(HttpClient client, int imageId, string imageUrl, CancellationToken cancellationToken)
        {
            try
            {
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

        private string GetImageIdFromUrl(string url)
        {
            try
            {
                // Extract the image ID from the URL (the number part)
                string fileName = url.Split('/').Last();
                string imageId = fileName.Split('.').First();
                return imageId;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error extracting image ID from URL: {Url}", url);
                // Return the URL if we can't extract the ID
                return url;
            }
        }

        // Event handlers for image interaction
        private void ImageBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (sender is FrameworkElement element && element.Tag is BackieeImage image)
                {
                    // Show image details or open in full screen
                    _logger?.LogInformation("Image clicked: {ImageId}", image.ImageId);
                    
                    // Create a context menu with options
                    var contextMenu = new ContextMenu();
                    
                    // Add view option
                    var viewMenuItem = new MenuItem { Header = "View fullscreen" };
                    viewMenuItem.Click += (s, args) => ViewImage_Click(image);
                    contextMenu.Items.Add(viewMenuItem);
                    
                    // Add save option
                    var saveMenuItem = new MenuItem { Header = "Save to disk" };
                    saveMenuItem.Click += (s, args) => SaveImage_Click(image);
                    contextMenu.Items.Add(saveMenuItem);
                    
                    // Add set as wallpaper option
                    var setAsWallpaperMenuItem = new MenuItem { Header = "Set as wallpaper" };
                    setAsWallpaperMenuItem.Click += (s, args) => SetAsWallpaper_Click(image);
                    contextMenu.Items.Add(setAsWallpaperMenuItem);
                    
                    // Show the context menu
                    contextMenu.IsOpen = true;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error handling image click");
            }
        }
        
        private void ViewImage_Click(BackieeImage image)
        {
            try
            {
                _logger?.LogInformation("ViewImage_Click called for image: {ImageId}", image.ImageId);
                
                // TODO: Implement full screen image viewing
                System.Windows.MessageBox.Show($"Viewing image: {image.ImageId}", "View Image", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in ViewImage_Click");
                System.Windows.MessageBox.Show($"Error viewing image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void SaveImage_Click(BackieeImage image)
        {
            try
            {
                _logger?.LogInformation("SaveImage_Click called for image: {ImageId}", image.ImageId);
                
                // TODO: Implement save image functionality
                System.Windows.MessageBox.Show($"Saving image: {image.ImageId}", "Save Image", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in SaveImage_Click");
                System.Windows.MessageBox.Show($"Error saving image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void SetAsWallpaper_Click(BackieeImage image)
        {
            try
            {
                _logger?.LogInformation("SetAsWallpaper_Click called for image: {ImageId}", image.ImageId);
                
                // TODO: Implement set as wallpaper functionality
                System.Windows.MessageBox.Show($"Setting as wallpaper: {image.ImageId}", "Set as Wallpaper", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in SetAsWallpaper_Click");
                System.Windows.MessageBox.Show($"Error setting as wallpaper: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadImagesFromJsonFile()
        {
            try
            {
                // Try multiple possible locations for the JSON file, prioritizing the root directory
                string[] possiblePaths = new string[]
                {
                    // Check the project root directory first (2 levels up from bin/Debug)
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "backiee_wallpapers.json"),
                    
                    // Check the root of the solution (3 levels up from bin/Debug)
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "..", "backiee_wallpapers.json"),
                    
                    // Check the current directory
                    "backiee_wallpapers.json",
                    
                    // Last resort: check the output directory 
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backiee_wallpapers.json")
                };

                string jsonPath = null;
                foreach (var path in possiblePaths)
                {
                    var fullPath = Path.GetFullPath(path);
                    _logger?.LogInformation($"Checking for JSON file at: {fullPath}");
                    
                    if (File.Exists(fullPath))
                    {
                        jsonPath = fullPath;
                        _logger?.LogInformation($"Found JSON file at: {jsonPath}");
                        break;
                    }
                }

                if (jsonPath == null)
                {
                    _logger?.LogError("JSON file not found in any of the checked locations");
                    
                    // Show a message to the user
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        System.Windows.MessageBox.Show(
                            "Could not find the wallpapers data file (backiee_wallpapers.json).\n\n" +
                            "Please ensure the file exists in the project root directory.",
                            "File Not Found",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    });
                    
                    return;
                }

                // Read the JSON content directly
                string jsonContent = await File.ReadAllTextAsync(jsonPath);
                
                // Use the simplest direct approach for the list of wallpapers
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
                        
                        // Get the AI status - most important part
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
                        
                        wallpapers.Add(wallpaper);
                    }
                }
                
                // Debug the first few
                for (int i = 0; i < Math.Min(wallpapers.Count, 5); i++)
                {
                    _logger?.LogInformation($"Wallpaper[{i}]: URL={wallpapers[i].Url}, Quality={wallpapers[i].Quality}, IsAI={wallpapers[i].IsAI}");
                }
                
                // Clear existing images
                Images.Clear();
                
                // Add the wallpapers to the UI
                foreach (var wallpaper in wallpapers)
                {
                    if (string.IsNullOrEmpty(wallpaper.Url))
                        continue;
                        
                    // Extract image ID from URL
                    string imageId = GetImageIdFromUrl(wallpaper.Url);
                    
                    // Create a BackieeImage object with properties set explicitly
                    var image = new BackieeImage();
                    
                    // Set basic properties
                    image.ImageUrl = wallpaper.Url;
                    image.ImageId = imageId;
                    image.IsLoading = false;
                    
                    // Explicitly set the AI status - this is critical
                    image.IsAI = wallpaper.IsAI;
                    
                    // Debug verification
                    _logger?.LogInformation($"Creating image: ID={imageId}, URL={wallpaper.Url}, AI={wallpaper.IsAI}, Image.IsAI={image.IsAI}");
                    
                    // Set resolution based on quality
                    image.Resolution = "1920x1080"; // Default
                    
                    if (!string.IsNullOrEmpty(wallpaper.Quality))
                    {
                        image.HasHighResolution = true;
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
                    
                    // Add to collection
                    Images.Add(image);
                }
                
                _logger?.LogInformation($"Successfully loaded {Images.Count} images from JSON file at {jsonPath}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading images from JSON file: " + ex.Message);
                
                // Show a message to the user
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    System.Windows.MessageBox.Show(
                        $"Error loading wallpapers: {ex.Message}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                });
            }
        }
        
        // Simple class to hold wallpaper data without any serialization complexities
        private class SimpleWallpaper
        {
            public string Url { get; set; } = "";
            public string Quality { get; set; } = "";
            public bool IsAI { get; set; }
        }
        
        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _logger?.LogInformation("FilterButton_Click called");
                
                // TODO: Implement filter functionality
                System.Windows.MessageBox.Show("Filter functionality not implemented yet", "Filter", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in FilterButton_Click");
                System.Windows.MessageBox.Show($"Error with filter: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void SetAsSlideshowButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _logger?.LogInformation("SetAsSlideshowButton_Click called");
                
                // TODO: Implement set as slideshow functionality
                System.Windows.MessageBox.Show("Set as slideshow functionality not implemented yet", "Set as Slideshow", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in SetAsSlideshowButton_Click");
                System.Windows.MessageBox.Show($"Error setting as slideshow: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class BackieeImage
    {
        private bool _isAI = false;
        
        public string ImageUrl { get; set; }
        public string ImageId { get; set; }
        public bool IsLoading { get; set; }
        public string Resolution { get; set; }
        
        // Properties for tags
        public bool HasHighResolution { get; set; }
        public string ResolutionLabel { get; set; }
        
        // Modified IsAI property with proper backing field and debug info
        public bool IsAI 
        { 
            get 
            { 
                Debug.WriteLine($"IsAI getter called for image {ImageId}, returning {_isAI}");
                return _isAI; 
            }
            set 
            { 
                Debug.WriteLine($"IsAI setter called for image {ImageId}, setting to {value}");
                _isAI = value; 
            }
        }

        // Add constructor to handle nullability warnings
        public BackieeImage()
        {
            ImageUrl = string.Empty;
            ImageId = string.Empty;
            Resolution = string.Empty;
            ResolutionLabel = string.Empty;
            
            // Initialize tag properties
            HasHighResolution = false;
            _isAI = false; // Ensure this is initialized
        }
        
        // Helper method to set resolution properties
        public void SetResolutionProperties()
        {
            if (string.IsNullOrEmpty(Resolution))
                return;
            
            // Parse resolution and set appropriate tag
            if (Resolution.Contains("x"))
            {
                var parts = Resolution.Split('x');
                if (parts.Length == 2 && int.TryParse(parts[0], out int width) && int.TryParse(parts[1], out int height))
                {
                    // For 4K resolution (3840x2160 or higher)
                    if (width >= 3840 && height >= 2160)
                    {
                        HasHighResolution = true;
                        
                        // For 8K resolution (7680x4320 or higher)
                        if (width >= 7680 && height >= 4320)
                        {
                            ResolutionLabel = "8K";
                        }
                        // For 5K resolution (5120x2880 or higher)
                        else if (width >= 5120 && height >= 2880)
                        {
                            ResolutionLabel = "5K";
                        }
                        else
                        {
                            ResolutionLabel = "4K";
                        }
                    }
                }
            }
        }
    }

    // Modified converter with more detailed debugging
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            Debug.WriteLine($"BooleanToVisibilityConverter called with value: {value} (type: {(value?.GetType()?.Name ?? "null")})");
            
            if (value is bool boolValue)
            {
                Debug.WriteLine($"Converting boolean {boolValue} to {(boolValue ? "Visible" : "Collapsed")}");
                return boolValue ? Visibility.Visible : Visibility.Collapsed;
            }
            
            Debug.WriteLine($"Value was not a boolean, returning Collapsed");
            return Visibility.Collapsed;
        }
        
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value is Visibility visibility && visibility == Visibility.Visible;
        }
    }

    internal class BackieeWallpaper
    {
        public string placeholder_url { get; set; } = string.Empty;
        public string real_page_url { get; set; } = string.Empty;
        public string quality { get; set; } = string.Empty;
        
        // Explicitly set the ai_status property to use plain bool
        public bool ai_status { get; set; }
        
        public int likes { get; set; }
        public int downloads { get; set; }
    }
} 