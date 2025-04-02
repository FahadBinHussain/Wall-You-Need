using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Wpf.Ui;
using Wpf.Ui.Controls;
using WallYouNeed.Core.Models;
using WallYouNeed.Core.Services.Interfaces;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Collections.Concurrent;

namespace WallYouNeed.App.Pages
{
    public partial class CategoryPage : Page, INavigableView<CategoryPage>
    {
        private readonly ILogger<CategoryPage> _logger;
        private readonly IWallpaperService _wallpaperService;
        private readonly ISnackbarService _snackbarService;
        private readonly IBackieeScraperService _backieeScraperService;
        private readonly ISettingsService _settingsService;
        
        private string _currentCategory = string.Empty;
        private volatile bool _isLoadingMore = false;
        private readonly SemaphoreSlim _loadingSemaphore = new SemaphoreSlim(1, 1);
        private readonly int _batchSize = 20;
        private readonly int _scrollThreshold = 400;
        private CancellationTokenSource _cts;
        private DateTime _lastScrollCheck = DateTime.MinValue;
        private readonly TimeSpan _scrollDebounceTime = TimeSpan.FromMilliseconds(250);
        private HashSet<string> _loadedUrls = new HashSet<string>();
        
        public ObservableCollection<Core.Models.Wallpaper> Wallpapers { get; } = new();
        public ObservableCollection<BackieeImage> Images { get; set; }
        
        public string CategoryTitle { get; private set; } = "Category";
        public string CategoryDescription { get; private set; } = "Browse wallpapers by category.";
        
        public CategoryPage ViewModel => this;
        
        public CategoryPage(
            ILogger<CategoryPage> logger,
            IWallpaperService wallpaperService,
            ISnackbarService snackbarService,
            IBackieeScraperService backieeScraperService,
            ISettingsService settingsService)
        {
            _logger = logger;
            _wallpaperService = wallpaperService;
            _snackbarService = snackbarService;
            _backieeScraperService = backieeScraperService;
            _settingsService = settingsService;
            
            InitializeComponent();
            Images = new ObservableCollection<BackieeImage>();
            DataContext = this;
        }
        
        public void SetCategory(string category)
        {
            _currentCategory = category;
            
            // Update the UI with the selected category
            switch (category.ToLowerInvariant())
            {
                // We've removed the "latest" case
                case "weekly":
                    CategoryTitle = "Weekly Picks";
                    CategoryDescription = "Our picks for this week's best wallpapers.";
                    break;
                case "monthly":
                    CategoryTitle = "Monthly Showcase";
                    CategoryDescription = "The most popular wallpapers from this month.";
                    break;
                case "nature":
                    CategoryTitle = "Nature";
                    CategoryDescription = "Beautiful landscapes, wildlife, and natural wonders.";
                    break;
                case "architecture":
                    CategoryTitle = "Architecture";
                    CategoryDescription = "Urban landscapes, buildings, and architectural wonders.";
                    break;
                case "abstract":
                    CategoryTitle = "Abstract";
                    CategoryDescription = "Creative, abstract, and artistic wallpapers.";
                    break;
                default:
                    CategoryTitle = $"{category}";
                    CategoryDescription = $"Wallpapers in the {category} category.";
                    break;
            }
            
            // Refresh UI to show the new title and description
            if (CategoryTitleTextBlock != null)
                CategoryTitleTextBlock.Text = CategoryTitle;
                
            if (CategoryDescriptionTextBlock != null)
                CategoryDescriptionTextBlock.Text = CategoryDescription;
            
            // Load wallpapers for this category when it's set
            LoadWallpapersForCategory(category);
        }
        
        private async void LoadWallpapersForCategory(string category)
        {
            try
            {
                _logger.LogInformation("Loading wallpapers for category: {Category}", category);
                
                // Show loading animation
                if (LoadingProgressRing != null)
                    LoadingProgressRing.Visibility = Visibility.Visible;
                
                // Clear existing wallpapers
                Wallpapers.Clear();
                
                // Also clear the UI panel
                if (WallpapersPanel != null)
                    WallpapersPanel.Children.Clear();
                
                // Handle different category types
                switch (category.ToLowerInvariant())
                {
                    case "latest":
                        // Special handling for Latest category
                        await LoadLatestWallpapers();
                        break;
                    case "weekly":
                    case "monthly":
                        // For demo purposes, we'll just use recent wallpapers
                        var recentWallpapers = await _wallpaperService.GetRecentWallpapersAsync(20);
                        foreach (var wallpaper in recentWallpapers)
                        {
                            Wallpapers.Add(wallpaper);
                            AddWallpaperCardToUI(wallpaper);
                        }
                        break;
                    default:
                        // For other categories, load existing wallpapers with matching tags
                        var wallpapers = await _wallpaperService.GetWallpapersByTagAsync(category);
                        foreach (var wallpaper in wallpapers)
                        {
                            Wallpapers.Add(wallpaper);
                            AddWallpaperCardToUI(wallpaper);
                        }
                        
                        // If no wallpapers are found, show a message
                        if (!wallpapers.Any())
                        {
                            _snackbarService.Show(
                                "Notice",
                                $"No wallpapers found in the {category} category.",
                                ControlAppearance.Info,
                                null,
                                TimeSpan.FromSeconds(3));
                            
                            // Show the empty state message
                            if (NoWallpapersMessage != null)
                                NoWallpapersMessage.Visibility = Visibility.Visible;
                        }
                        break;
                }
                
                _logger.LogInformation("Loaded {Count} wallpapers for category: {Category}", 
                    Wallpapers.Count, category);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading wallpapers for category: {Category}", category);
                
                _snackbarService.Show(
                    "Error",
                    $"Failed to load wallpapers for {category}",
                    ControlAppearance.Danger,
                    null,
                    TimeSpan.FromSeconds(3));
            }
            finally
            {
                // Hide loading animation when done
                if (LoadingProgressRing != null)
                    LoadingProgressRing.Visibility = Visibility.Collapsed;
            }
        }
        
        // New method for loading latest wallpapers
        private async Task LoadLatestWallpapers()
        {
            _logger.LogInformation("Loading latest wallpapers using static Backiee images");
            
            try
            {
                // Get static placeholder wallpapers from the BackieeScraperService
                var placeholderWallpapers = _backieeScraperService.GeneratePlaceholderWallpapers(20);
                _logger.LogInformation("Generated {Count} static placeholder wallpapers", placeholderWallpapers.Count);
                
                // For Latest, create the UI cards directly without adding to collection or repository
                foreach (var model in placeholderWallpapers)
                {
                    _logger.LogDebug("Processing wallpaper ID: {Id}, URL: {Url}", model.Id, model.ThumbnailUrl);
                    
                    // Create a card directly in the UI
                    CreateDirectWallpaperCard(model);
                }
                
                // Show success message
                _snackbarService.Show(
                    "Success",
                    "Latest wallpapers loaded successfully",
                    ControlAppearance.Success,
                    null,
                    TimeSpan.FromSeconds(2));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading latest wallpapers");
                throw;
            }
        }
        
        // Create a wallpaper card directly from the model without using the repository
        private void CreateDirectWallpaperCard(WallpaperModel model)
        {
            try
            {
                if (WallpapersPanel == null)
                {
                    _logger.LogWarning("WallpapersPanel is null, cannot add wallpaper card to UI");
                    return;
                }
                
                _logger.LogInformation("Creating direct wallpaper card for ID: {Id}, Thumbnail: {Url}", 
                    model.Id, model.ThumbnailUrl);
                
                // Create the card
                var card = new Wpf.Ui.Controls.Card
                {
                    Margin = new Thickness(8),
                    Width = 280,
                    Height = 200
                };
                
                // Create the grid to hold the image and info
                var grid = new Grid();
                
                // Create the image
                var image = new System.Windows.Controls.Image
                {
                    Stretch = Stretch.UniformToFill
                };
                
                bool imageLoaded = false;
                
                // Load the image from URL using BitmapImage
                try
                {
                    _logger.LogDebug("Attempting to load image from thumbnail URL: {Url}", model.ThumbnailUrl);
                    
                    // Create a bitmap image
                    var bitmap = new BitmapImage();
                    
                    // Important: Set CacheOption before BeginInit
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.BeginInit();
                    
                    // Add download failed event handler
                    bitmap.DownloadFailed += (s, e) => {
                        _logger.LogError("Image download failed: {Error}", e.ErrorException?.Message ?? "Unknown error");
                    };
                    
                    // Create an absolute URI
                    bitmap.UriSource = new Uri(model.ThumbnailUrl, UriKind.Absolute);
                    
                    // End initialization
                    bitmap.EndInit();
                    
                    // Set the image source
                    image.Source = bitmap;
                    imageLoaded = true;
                    
                    _logger.LogInformation("Successfully loaded image from URL: {Url}", model.ThumbnailUrl);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load thumbnail URL: {Url}", model.ThumbnailUrl);
                    
                    try
                    {
                        // Try the full image URL as a fallback
                        _logger.LogDebug("Trying full image URL instead: {Url}", model.ImageUrl);
                        
                        var bitmap = new BitmapImage();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(model.ImageUrl, UriKind.Absolute);
                        bitmap.DownloadFailed += (s, e) => {
                            _logger.LogError("Image download failed: {Error}", e.ErrorException?.Message ?? "Unknown error");
                        };
                        bitmap.EndInit();
                        
                        image.Source = bitmap;
                        imageLoaded = true;
                        
                        _logger.LogInformation("Successfully loaded image from full URL: {Url}", model.ImageUrl);
                    }
                    catch (Exception innerEx)
                    {
                        _logger.LogError(innerEx, "Failed to load image from full URL: {Url}", model.ImageUrl);
                        
                        // Use fallback color with text if both image loads fail
                        var fallbackBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(96, 125, 139));
                        
                        // Create fallback rectangle (use Border instead of Rectangle for WPF)
                        var fallbackBorder = new Border
                        {
                            Background = fallbackBrush,
                            Width = 280,
                            Height = 200
                        };
                        
                        var fallbackText = new System.Windows.Controls.TextBlock
                        {
                            Text = $"ID: {model.Id}",
                            Foreground = System.Windows.Media.Brushes.White,
                            FontSize = 16,
                            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                            VerticalAlignment = System.Windows.VerticalAlignment.Center
                        };
                        
                        // Add the border and text to the grid
                        grid.Children.Add(fallbackBorder);
                        grid.Children.Add(fallbackText);
                    }
                }
                
                // Only add the image if it was loaded successfully
                if (imageLoaded)
                {
                    grid.Children.Add(image);
                }
                
                // Create the info panel at the bottom
                var infoBorder = new Border
                {
                    Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(176, 0, 0, 0)), // #B0000000
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Padding = new Thickness(10, 8, 10, 8)
                };
                
                var infoPanel = new StackPanel();
                
                // Title
                var title = new System.Windows.Controls.TextBlock
                {
                    Text = model.Title ?? "Untitled Wallpaper",
                    Foreground = System.Windows.Media.Brushes.White,
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold
                };
                
                // Source info
                var source = new System.Windows.Controls.TextBlock
                {
                    Text = $"Source: {model.Source ?? "Unknown"}",
                    Foreground = System.Windows.Media.Brushes.LightGray,
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
                
                // Apply button
                var applyButton = new Wpf.Ui.Controls.Button
                {
                    Content = "View",
                    Appearance = Wpf.Ui.Controls.ControlAppearance.Primary,
                    Padding = new Thickness(8, 4, 8, 4),
                    Margin = new Thickness(0, 0, 4, 0),
                    FontSize = 12
                };
                
                applyButton.Click += (s, e) => ViewWallpaperFromModel(model);
                
                // Save button
                var saveButton = new Wpf.Ui.Controls.Button
                {
                    Content = "Save",
                    Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary,
                    Padding = new Thickness(8, 4, 8, 4),
                    Margin = new Thickness(4, 0, 4, 0),
                    FontSize = 12
                };
                
                saveButton.Click += (s, e) => SaveWallpaperFromModel(model);
                
                // Add buttons to panel
                buttonsPanel.Children.Add(applyButton);
                buttonsPanel.Children.Add(saveButton);
                
                // Add elements to info panel
                infoPanel.Children.Add(title);
                infoPanel.Children.Add(source);
                infoPanel.Children.Add(buttonsPanel);
                
                // Add info panel to border
                infoBorder.Child = infoPanel;
                
                // Add border to grid
                grid.Children.Add(infoBorder);
                
                // Add grid to card
                card.Content = grid;
                
                // Add click event to the card
                card.MouseLeftButtonUp += (s, e) => ViewWallpaperFromModel(model);
                
                // Add card to the panel
                WallpapersPanel.Children.Add(card);
                
                _logger.LogInformation("Added direct wallpaper card to UI for ID: {Id}", model.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating direct wallpaper card for ID: {Id}", model.Id);
            }
        }
        
        // Helper methods for the direct card actions
        private void ViewWallpaperFromModel(WallpaperModel model)
        {
            _logger.LogInformation("View wallpaper clicked: {Id}", model.Id);
            
            Process.Start(new ProcessStartInfo
            {
                FileName = model.ImageUrl,
                UseShellExecute = true
            });
        }
        
        private async void SaveWallpaperFromModel(WallpaperModel model)
        {
            _logger.LogInformation("Save wallpaper clicked: {Id}", model.Id);
            
            try
            {
                // Convert to Wallpaper model
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
                        { "Source", model.Source },
                        { "Rating", model.Rating.ToString() },
                        { "Resolution", model.ResolutionCategory },
                        { "IsStaticPlaceholder", "true" }
                    }
                };
                
                // Save to repository
                await _wallpaperService.SaveWallpaperAsync(wallpaper);
                
                _snackbarService.Show(
                    "Success",
                    "Wallpaper saved to your collection",
                    ControlAppearance.Success,
                    null,
                    TimeSpan.FromSeconds(2));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving wallpaper: {Id}", model.Id);
                
                _snackbarService.Show(
                    "Error",
                    "Failed to save wallpaper",
                    ControlAppearance.Danger,
                    null,
                    TimeSpan.FromSeconds(2));
            }
        }
        
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Navigate back to the home page
                if (Window.GetWindow(this) is MainWindow mainWindow &&
                    mainWindow.FindName("MainFrame") is Frame mainFrame)
                {
                    if (mainFrame.CanGoBack)
                    {
                        mainFrame.GoBack();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error navigating back from category page");
                
                _snackbarService.Show(
                    "Error",
                    "Failed to navigate back",
                    ControlAppearance.Danger,
                    null,
                    TimeSpan.FromSeconds(2));
            }
        }
        
        // Placeholder implementations for XAML event handlers to avoid build errors
        private void SortButton_Click(object sender, RoutedEventArgs e)
        {
            _snackbarService.Show(
                "Info",
                "Sorting functionality is not implemented yet.",
                ControlAppearance.Info,
                null,
                TimeSpan.FromSeconds(2));
        }
        
        private void AddWallpaperButton_Click(object sender, RoutedEventArgs e)
        {
            _snackbarService.Show(
                "Info",
                "Add wallpaper functionality is not implemented yet.",
                ControlAppearance.Info,
                null,
                TimeSpan.FromSeconds(2));
        }
        
        // Add a new method to create and add wallpaper cards to the UI
        private void AddWallpaperCardToUI(Core.Models.Wallpaper wallpaper)
        {
            try
            {
                if (WallpapersPanel == null)
                {
                    _logger.LogWarning("WallpapersPanel is null, cannot add wallpaper card to UI");
                    return;
                }
                
                _logger.LogInformation("Creating wallpaper card for ID: {Id}, Title: {Title}", wallpaper.Id, wallpaper.Title);
                _logger.LogDebug("Thumbnail URL: {ThumbnailUrl}", wallpaper.ThumbnailUrl);
                _logger.LogDebug("Source URL: {SourceUrl}", wallpaper.SourceUrl);
                
                // Create the card
                var card = new Wpf.Ui.Controls.Card
                {
                    Margin = new Thickness(8),
                    Width = 280,
                    Height = 200
                };
                
                // Create the grid to hold the image and info
                var grid = new Grid();
                
                // Create the image
                var image = new System.Windows.Controls.Image
                {
                    Stretch = Stretch.UniformToFill
                };
                
                bool imageLoaded = false;
                
                // Load the image from URL
                try
                {
                    _logger.LogDebug("Attempting to load thumbnail URL: {Url}", wallpaper.ThumbnailUrl);
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    
                    // Add a handler for DownloadFailed event
                    bitmap.DownloadFailed += (s, e) => {
                        _logger.LogError("Image download failed for URL: {Url}, Error: {Error}", 
                            wallpaper.ThumbnailUrl, e.ErrorException?.Message ?? "Unknown error");
                    };
                    
                    // Create a new Uri with UriKind.Absolute to ensure it's treated as an absolute URL
                    bitmap.UriSource = new Uri(wallpaper.ThumbnailUrl, UriKind.Absolute);
                    bitmap.EndInit();
                    
                    image.Source = bitmap;
                    imageLoaded = true;
                    _logger.LogInformation("Successfully loaded thumbnail from URL: {Url}", wallpaper.ThumbnailUrl);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading thumbnail for wallpaper {Id}: {Url}", 
                        wallpaper.Id, wallpaper.ThumbnailUrl);
                    
                    // Try the source URL if thumbnail fails
                    try
                    {
                        _logger.LogDebug("Attempting to load source URL: {Url}", wallpaper.SourceUrl);
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        
                        // Add a handler for DownloadFailed event
                        bitmap.DownloadFailed += (s, e) => {
                            _logger.LogError("Image download failed for URL: {Url}, Error: {Error}", 
                                wallpaper.SourceUrl, e.ErrorException?.Message ?? "Unknown error");
                        };
                        
                        // Create a new Uri with UriKind.Absolute to ensure it's treated as an absolute URL
                        bitmap.UriSource = new Uri(wallpaper.SourceUrl, UriKind.Absolute);
                        bitmap.EndInit();
                        
                        image.Source = bitmap;
                        imageLoaded = true;
                        _logger.LogInformation("Successfully loaded image from source URL: {Url}", wallpaper.SourceUrl);
                    }
                    catch (Exception innerEx)
                    {
                        _logger.LogError(innerEx, "Also failed to load source URL: {Url}", wallpaper.SourceUrl);
                        
                        // Create a very simple colored rectangle with text as a fallback
                        DrawFallbackImage(image, wallpaper);
                    }
                }
                
                // Create the info panel at the bottom
                var infoBorder = new Border
                {
                    Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(176, 0, 0, 0)), // #B0000000
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Padding = new Thickness(10, 8, 10, 8)
                };
                
                var infoPanel = new StackPanel();
                
                // Title
                var title = new System.Windows.Controls.TextBlock
                {
                    Text = wallpaper.Title ?? "Untitled Wallpaper",
                    Foreground = System.Windows.Media.Brushes.White,
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold
                };
                
                // Source info
                string sourceInfo = "Source: Unknown";
                if (wallpaper.Metadata != null && wallpaper.Metadata.ContainsKey("Source"))
                {
                    sourceInfo = $"Source: {wallpaper.Metadata["Source"]}";
                }
                
                var source = new System.Windows.Controls.TextBlock
                {
                    Text = sourceInfo,
                    Foreground = System.Windows.Media.Brushes.LightGray,
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
                
                // Apply button
                var applyButton = new Wpf.Ui.Controls.Button
                {
                    Content = "Apply",
                    Appearance = Wpf.Ui.Controls.ControlAppearance.Primary,
                    Padding = new Thickness(8, 4, 8, 4),
                    Margin = new Thickness(0, 0, 4, 0),
                    FontSize = 12
                };
                
                applyButton.Click += (s, e) => ApplyWallpaper(wallpaper);
                
                // Favorite button
                var favoriteButton = new Wpf.Ui.Controls.Button
                {
                    Content = "♡",
                    Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary,
                    Padding = new Thickness(8, 4, 8, 4),
                    Margin = new Thickness(4, 0, 4, 0),
                    FontSize = 12
                };
                
                favoriteButton.Click += (s, e) => ToggleFavorite(wallpaper);
                
                // More options button
                var moreButton = new Wpf.Ui.Controls.Button
                {
                    Content = "⋮",
                    Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary,
                    Padding = new Thickness(8, 4, 8, 4),
                    Margin = new Thickness(4, 0, 0, 0),
                    FontSize = 12
                };
                
                moreButton.Click += (s, e) => ShowMoreOptions(wallpaper);
                
                // Add buttons to panel
                buttonsPanel.Children.Add(applyButton);
                buttonsPanel.Children.Add(favoriteButton);
                buttonsPanel.Children.Add(moreButton);
                
                // Add elements to info panel
                infoPanel.Children.Add(title);
                infoPanel.Children.Add(source);
                infoPanel.Children.Add(buttonsPanel);
                
                // Add info panel to border
                infoBorder.Child = infoPanel;
                
                // Add elements to grid
                grid.Children.Add(image);
                grid.Children.Add(infoBorder);
                
                // Add grid to card
                card.Content = grid;
                
                // Add click event to the card for viewing details
                card.MouseLeftButtonUp += (s, e) => ViewWallpaperDetails(wallpaper);
                
                // Add card to the panel
                WallpapersPanel.Children.Add(card);
                
                // Log success
                _logger.LogInformation("Added wallpaper card to UI for wallpaper ID: {Id}, Image loaded: {ImageLoaded}", 
                    wallpaper.Id, imageLoaded);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding wallpaper card to UI for wallpaper {Id}", wallpaper.Id);
            }
        }
        
        // Helper method to create a fallback image when loading fails
        private void DrawFallbackImage(System.Windows.Controls.Image imageControl, Core.Models.Wallpaper wallpaper)
        {
            try
            {
                _logger.LogInformation("Creating fallback image for wallpaper ID: {Id}", wallpaper.Id);
                
                // Create a drawing visual
                var drawingVisual = new DrawingVisual();
                using (var drawingContext = drawingVisual.RenderOpen())
                {
                    // Draw a colored background
                    drawingContext.DrawRectangle(
                        new SolidColorBrush(System.Windows.Media.Color.FromRgb(96, 125, 139)), // Material Design Blue Gray 500
                        null,
                        new Rect(0, 0, 280, 200));
                    
                    // Draw the wallpaper ID text
                    var idText = new FormattedText(
                        $"ID: {wallpaper.Id}",
                        System.Globalization.CultureInfo.CurrentCulture,
                        System.Windows.FlowDirection.LeftToRight,
                        new Typeface("Segoe UI"),
                        14,
                        System.Windows.Media.Brushes.White,
                        VisualTreeHelper.GetDpi(drawingVisual).PixelsPerDip);
                    
                    drawingContext.DrawText(idText, new System.Windows.Point(10, 10));
                    
                    // Draw the error message
                    var errorText = new FormattedText(
                        "Image Failed to Load",
                        System.Globalization.CultureInfo.CurrentCulture,
                        System.Windows.FlowDirection.LeftToRight,
                        new Typeface("Segoe UI Semibold"),
                        18,
                        System.Windows.Media.Brushes.White,
                        VisualTreeHelper.GetDpi(drawingVisual).PixelsPerDip);
                    
                    // Center the text
                    drawingContext.DrawText(
                        errorText, 
                        new System.Windows.Point((280 - errorText.Width) / 2, (200 - errorText.Height) / 2));
                    
                    // Draw the URL hint at the bottom
                    var urlText = new FormattedText(
                        "Check URL format in logs",
                        System.Globalization.CultureInfo.CurrentCulture,
                        System.Windows.FlowDirection.LeftToRight,
                        new Typeface("Segoe UI"),
                        12,
                        System.Windows.Media.Brushes.White,
                        VisualTreeHelper.GetDpi(drawingVisual).PixelsPerDip);
                    
                    drawingContext.DrawText(urlText, new System.Windows.Point(10, 170));
                }
                
                // Convert drawing to bitmap
                var renderTarget = new RenderTargetBitmap(
                    280, 200, 96, 96, PixelFormats.Pbgra32);
                renderTarget.Render(drawingVisual);
                
                // Set as image source
                imageControl.Source = renderTarget;
                
                _logger.LogInformation("Fallback image created successfully for wallpaper ID: {Id}", wallpaper.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating fallback image for wallpaper ID: {Id}", wallpaper.Id);
            }
        }
        
        // Placeholder methods for wallpaper actions (to be implemented later)
        private void ApplyWallpaper(Core.Models.Wallpaper wallpaper)
        {
            _logger.LogInformation("Apply wallpaper clicked: {Id}", wallpaper.Id);
            _snackbarService.Show(
                "Info",
                $"Apply wallpaper feature is not implemented yet.",
                ControlAppearance.Info,
                null,
                TimeSpan.FromSeconds(2));
        }
        
        private void ToggleFavorite(Core.Models.Wallpaper wallpaper)
        {
            _logger.LogInformation("Toggle favorite clicked: {Id}", wallpaper.Id);
            _snackbarService.Show(
                "Info",
                $"Favorite wallpaper feature is not implemented yet.",
                ControlAppearance.Info,
                null,
                TimeSpan.FromSeconds(2));
        }
        
        private void ShowMoreOptions(Core.Models.Wallpaper wallpaper)
        {
            _logger.LogInformation("More options clicked: {Id}", wallpaper.Id);
            _snackbarService.Show(
                "Info",
                $"More options feature is not implemented yet.",
                ControlAppearance.Info,
                null,
                TimeSpan.FromSeconds(2));
        }
        
        private void ViewWallpaperDetails(Core.Models.Wallpaper wallpaper)
        {
            _logger.LogInformation("View wallpaper details clicked: {Id}", wallpaper.Id);
            _snackbarService.Show(
                "Info",
                $"View wallpaper details feature is not implemented yet.",
                ControlAppearance.Info,
                null,
                TimeSpan.FromSeconds(2));
        }

        private void ConvertAndAddWallpaper(Core.Models.Wallpaper wallpaper)
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
                var wallpapers = JsonSerializer.Deserialize<List<Core.Models.Wallpaper>>(jsonContent);

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