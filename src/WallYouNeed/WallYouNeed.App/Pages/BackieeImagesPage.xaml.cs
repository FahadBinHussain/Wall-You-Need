using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;
using System.Net.Http;

namespace WallYouNeed.App.Pages
{
    /// <summary>
    /// Interaction logic for BackieeImagesPage.xaml
    /// </summary>
    public partial class BackieeImagesPage : Page
    {
        private ObservableCollection<BackieeImage> Images { get; set; }
        private ILogger<BackieeImagesPage> _logger;

        public BackieeImagesPage(ILogger<BackieeImagesPage> logger = null)
        {
            try
            {
                _logger = logger;
                _logger?.LogInformation("BackieeImagesPage constructor called");
                
                InitializeComponent();
                
                Images = new ObservableCollection<BackieeImage>();
                ImagesItemsControl.ItemsSource = Images;
                
                // Load images asynchronously
                Loaded += BackieeImagesPage_Loaded;
                
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
                await FetchAndDisplayImages();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in BackieeImagesPage_Loaded event handler");
                System.Windows.MessageBox.Show($"Error loading BackieeImagesPage: {ex.Message}", 
                    "Loading Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task FetchAndDisplayImages()
        {
            try
            {
                _logger?.LogInformation("FetchAndDisplayImages called");
                
                // Show loading indicator or message
                System.Windows.Application.Current.Dispatcher.Invoke(() => {
                    Images.Clear();
                });
                
                List<BackieeImage> fetchedImages = new List<BackieeImage>();
                
                // Use the BackieeScraper to fetch the latest images
                await Task.Run(async () => {
                    try
                    {
                        _logger?.LogInformation("Starting to fetch images from Backiee website");
                        
                        // Create HTTP client
                        using (HttpClient client = new HttpClient())
                        {
                            // Add headers to simulate a browser request
                            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/134.0.0.0 Safari/537.36");
                            client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
                            
                            // Fetch the main page
                            _logger?.LogInformation("Fetching content from backiee.com...");
                            HttpResponseMessage response = await client.GetAsync("https://backiee.com");
                            
                            if (!response.IsSuccessStatusCode)
                            {
                                throw new Exception($"Failed to fetch the webpage. Status code: {response.StatusCode}");
                            }

                            // Read the content
                            string html = await response.Content.ReadAsStringAsync();
                            _logger?.LogInformation($"Content fetched successfully. Length: {html.Length} characters");

                            // Extract the image URLs using regex
                            string pattern = @"<div class=""placeholder""[^>]*?>\s*<img[^>]*?data-src=""([^""]+)""";
                            var matches = System.Text.RegularExpressions.Regex.Matches(html, pattern);
                            
                            foreach (System.Text.RegularExpressions.Match match in matches)
                            {
                                string imageUrl = match.Groups[1].Value;
                                if (!string.IsNullOrEmpty(imageUrl))
                                {
                                    // Extract the image ID from the URL
                                    string imageId = GetImageIdFromUrl(imageUrl);
                                    
                                    fetchedImages.Add(new BackieeImage
                                    {
                                        ImageUrl = imageUrl,
                                        ImageId = imageId
                                    });
                                }
                            }
                            
                            _logger?.LogInformation($"Extracted {fetchedImages.Count} images from the webpage");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Error fetching images from Backiee website");
                        
                        // Fallback to reading from the markdown file if the fetch fails
                        _logger?.LogInformation("Falling back to reading from static markdown file");
                        await LoadImagesFromMarkdownFile();
                        return;
                    }
                });
                
                // Add the fetched images to our collection
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var image in fetchedImages)
                    {
                        Images.Add(image);
                    }
                });
                
                _logger?.LogInformation("Successfully added {Count} images to the collection", Images.Count);
                
                // Save the fetched images to the markdown file for backup
                await Task.Run(() => {
                    try
                    {
                        string mdFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backiee_static_images.md");
                        
                        using (StreamWriter writer = new StreamWriter(mdFilePath))
                        {
                            foreach (var image in fetchedImages)
                            {
                                writer.WriteLine(image.ImageUrl);
                            }
                        }
                        
                        _logger?.LogInformation("Successfully saved fetched images to markdown file as backup");
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Error saving fetched images to markdown file");
                        // Continue anyway, as this is just a backup operation
                    }
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in FetchAndDisplayImages");
                System.Windows.MessageBox.Show($"Error fetching images: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                
                // Fallback to reading from the markdown file
                await LoadImagesFromMarkdownFile();
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
                    _logger?.LogInformation("Image clicked: {ImageId}", image.ImageId);
                    
                    // Open the image in default browser
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = image.ImageUrl,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error handling image click");
                System.Windows.MessageBox.Show($"Error opening image: {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void ViewImage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is FrameworkElement element && element.Tag is BackieeImage image)
                {
                    _logger?.LogInformation("View button clicked for image: {ImageId}", image.ImageId);
                    
                    // Open the image in default browser
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = image.ImageUrl,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error handling view button click");
                System.Windows.MessageBox.Show($"Error viewing image: {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void SaveImage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is FrameworkElement element && element.Tag is BackieeImage image)
                {
                    _logger?.LogInformation("Save button clicked for image: {ImageId}", image.ImageId);
                    
                    // Show a message since we don't have access to the WallpaperService here
                    System.Windows.MessageBox.Show($"Image {image.ImageId} would be saved to your collection.", 
                        "Save Image", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error handling save button click");
                System.Windows.MessageBox.Show($"Error saving image: {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        // Top action buttons event handlers
        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _logger?.LogInformation("Filter button clicked");
                // Placeholder for filter functionality
                System.Windows.MessageBox.Show("Filter functionality will be implemented in a future update.", 
                    "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error handling filter button click");
                System.Windows.MessageBox.Show($"Error: {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void SetAsSlideshowButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _logger?.LogInformation("Set as slideshow button clicked");
                // Placeholder for slideshow functionality
                System.Windows.MessageBox.Show("Slideshow functionality will be implemented in a future update.", 
                    "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error handling set as slideshow button click");
                System.Windows.MessageBox.Show($"Error: {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Keep the LoadImagesFromMarkdownFile method as a fallback
        private async Task LoadImagesFromMarkdownFile()
        {
            try
            {
                _logger?.LogInformation("LoadImagesFromMarkdownFile called");
                
                // Path to the markdown file relative to the solution directory
                string mdFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "backiee_static_images.md");
                _logger?.LogDebug("Attempting to load markdown file from path: {Path}", mdFilePath);
                
                // Check if file exists and can be accessed
                if (!File.Exists(mdFilePath))
                {
                    // Try an alternative path
                    mdFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backiee_static_images.md");
                    _logger?.LogDebug("First path not found, trying alternative path: {Path}", mdFilePath);
                    
                    if (!File.Exists(mdFilePath))
                    {
                        // Try one more absolute path as a last resort
                        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                        var solutionDir = Directory.GetParent(baseDir)?.Parent?.Parent?.Parent?.Parent?.FullName;
                        
                        if (solutionDir != null)
                        {
                            mdFilePath = Path.Combine(solutionDir, "backiee_static_images.md");
                            _logger?.LogDebug("Second path not found, trying solution dir path: {Path}", mdFilePath);
                        }
                        
                        if (!File.Exists(mdFilePath))
                        {
                            _logger?.LogError("Could not find the backiee_static_images.md file at any attempted location");
                            System.Windows.MessageBox.Show($"Could not find the backiee_static_images.md file. Attempted paths:\n" + 
                                $"1. {Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "backiee_static_images.md")}\n" +
                                $"2. {Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backiee_static_images.md")}\n" +
                                $"3. {mdFilePath}",
                                "File Not Found", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                    }
                }

                _logger?.LogInformation("Found markdown file at: {Path}", mdFilePath);

                // Read all lines from the markdown file
                string[] imageUrls = await Task.Run(() => File.ReadAllLines(mdFilePath));
                _logger?.LogInformation("Read {Count} lines from markdown file", imageUrls.Length);

                // Process each URL
                foreach (string url in imageUrls)
                {
                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        // Extract the image ID from the URL
                        string imageId = GetImageIdFromUrl(url);
                        
                        // Add the image to our collection
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            Images.Add(new BackieeImage
                            {
                                ImageUrl = url.Trim(),
                                ImageId = imageId
                            });
                        });
                    }
                }
                
                _logger?.LogInformation("Successfully added {Count} images to the collection", Images.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading images from markdown file");
                System.Windows.MessageBox.Show($"Error loading images: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class BackieeImage
    {
        public string ImageUrl { get; set; }
        public string ImageId { get; set; }
    }
} 