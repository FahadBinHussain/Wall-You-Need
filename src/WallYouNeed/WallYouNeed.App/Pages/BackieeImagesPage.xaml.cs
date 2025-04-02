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
                
                // Create a value converter for boolean to visibility
                Resources.Add("BooleanToVisibilityConverter", new BooleanToVisibilityConverter());
                
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
                
                // First try to get the initial images to determine highest image ID
                await FetchInitialImages();
                
                // Then start the infinite loading
                await LoadMoreImagesAsync(_cts.Token);
                
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
                
                List<BackieeImage> fetchedImages = new List<BackieeImage>();
                
                await Task.Run(async () => {
                    try
                    {
                        _logger?.LogInformation("Starting to fetch images from Backiee website");
                        
                        using (HttpClient client = new HttpClient())
                        {
                            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/134.0.0.0 Safari/537.36");
                            client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8");
                            
                            _logger?.LogInformation("Fetching content from backiee.com...");
                            HttpResponseMessage response = await client.GetAsync("https://backiee.com");
                            
                            if (!response.IsSuccessStatusCode)
                            {
                                throw new Exception($"Failed to fetch the webpage. Status code: {response.StatusCode}");
                            }

                            string html = await response.Content.ReadAsStringAsync();
                            _logger?.LogInformation($"Content fetched successfully. Length: {html.Length} characters");

                            // Extract image information using a more comprehensive regex pattern
                            string pattern = @"<div class=""placeholder""[^>]*?>\s*<img[^>]*?data-src=""([^""]+)""[^>]*?>\s*";

                            var matches = System.Text.RegularExpressions.Regex.Matches(html, pattern, System.Text.RegularExpressions.RegexOptions.Singleline);
                            
                            // Create a list to maintain order
                            var orderedImages = new List<BackieeImage>();
                            
                            foreach (System.Text.RegularExpressions.Match match in matches)
                            {
                                string imageUrl = match.Groups[1].Value;
                                if (!string.IsNullOrEmpty(imageUrl))
                                {
                                    string imageId = GetImageIdFromUrl(imageUrl);
                                    
                                    var newImage = new BackieeImage
                                    {
                                        ImageUrl = imageUrl,
                                        ImageId = imageId,
                                        IsLoading = false,
                                        Resolution = "1920x1080"
                                    };
                                    
                                    _logger?.LogDebug($"Adding image: ID={imageId}, Resolution={newImage.Resolution}");
                                    orderedImages.Add(newImage);
                                    
                                    if (int.TryParse(imageId, out int id))
                                    {
                                        _attemptedIds.Add(id);
                                    }
                                }
                            }
                            
                            _logger?.LogInformation($"Extracted {orderedImages.Count} images from the webpage. Highest ID: {_currentImageId}");
                            
                            // Add the images in the exact order they were found
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                foreach (var image in orderedImages)
                                {
                                    if (!_loadedUrls.Contains(image.ImageUrl))
                                    {
                                        Images.Add(image);
                                        _loadedUrls.Add(image.ImageUrl);
                                    }
                                }
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Error fetching images from Backiee website");
                        _logger?.LogInformation("Falling back to reading from JSON file");
                        await LoadImagesFromJsonFile();
                        return;
                    }
                });
                
                _logger?.LogInformation("Successfully added {Count} initial images to the collection", Images.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in FetchInitialImages");
                System.Windows.MessageBox.Show($"Error fetching initial images: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                await LoadImagesFromJsonFile();
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

                                batchImages.Add(new BackieeImage
                                {
                                    ImageUrl = imageUrl,
                                    ImageId = id.ToString(),
                                    IsLoading = false,
                                    Resolution = "1920x1080"
                                });
                                
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
                string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backiee_wallpapers.json");
                _logger?.LogInformation($"Loading images from JSON file: {jsonPath}");

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
                    if (!string.IsNullOrEmpty(wallpaper.ThumbnailUrl))
                    {
                        var backieeImage = new BackieeImage
                        {
                            ImageUrl = wallpaper.ThumbnailUrl,
                            ImageId = wallpaper.Id,
                            Resolution = $"{wallpaper.Width}x{wallpaper.Height}"
                        };

                        Images.Add(backieeImage);
                    }
                }

                _logger?.LogInformation($"Successfully loaded {Images.Count} images from JSON");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading images from JSON file");
            }
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
        public string ImageUrl { get; set; }
        public string ImageId { get; set; }
        public bool IsLoading { get; set; }
        public string Resolution { get; set; }

        // Add constructor to handle nullability warnings
        public BackieeImage()
        {
            ImageUrl = string.Empty;
            ImageId = string.Empty;
            Resolution = string.Empty;
        }
    }

    // Value converter for binding boolean values to visibility
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }
        
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                return visibility == Visibility.Visible;
            }
            return false;
        }
    }
} 