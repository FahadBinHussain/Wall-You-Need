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
using System.Collections.Generic;

namespace WallYouNeed.App.Pages
{
    public partial class HomePage : Page, INavigableView<HomePage>
    {
        private readonly ILogger<HomePage> _logger;
        private readonly ISnackbarService _snackbarService;
        private readonly IWallpaperService _wallpaperService;
        private readonly ICollectionService _collectionService;
        private readonly ISettingsService _settingsService;

        public ObservableCollection<Wallpaper> RecentWallpapers { get; } = new();
        public ObservableCollection<Wallpaper> FavoriteWallpapers { get; } = new();
        public ObservableCollection<Wallpaper> CurrentWallpaper { get; } = new();

        public HomePage ViewModel => this;

        public HomePage(
            ILogger<HomePage> logger,
            ISnackbarService snackbarService,
            IWallpaperService wallpaperService,
            ICollectionService collectionService,
            ISettingsService settingsService)
        {
            _logger = logger;
            _snackbarService = snackbarService;
            _wallpaperService = wallpaperService;
            _collectionService = collectionService;
            _settingsService = settingsService;

            InitializeComponent();
            DataContext = this;

            Loaded += HomePage_Loaded;
        }

        private async void HomePage_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Set a dark background color for better contrast
                this.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 30));

                // Set text colors for better visibility
                foreach (System.Windows.Controls.TextBlock textBlock in FindVisualChildren<System.Windows.Controls.TextBlock>(this))
                {
                    if (textBlock.Foreground == null || textBlock.Foreground == System.Windows.Media.Brushes.Black)
                    {
                        textBlock.Foreground = System.Windows.Media.Brushes.White;
                    }
                }

                await LoadStatisticsAsync();
                await LoadRecentWallpapersAsync();
                await LoadCurrentWallpaperAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load home page");
                _snackbarService.Show("Error", "Failed to load home page", Wpf.Ui.Controls.ControlAppearance.Danger, null, TimeSpan.FromSeconds(2));
            }
        }

        // Helper method to find visual children of a specific type
        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T)
                    {
                        yield return (T)child;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }

        private async Task LoadStatisticsAsync()
        {
            try
            {
                var wallpapers = await _wallpaperService.GetAllWallpapersAsync();
                var favorites = await _wallpaperService.GetFavoriteWallpapersAsync();
                var collections = await _collectionService.GetAllCollectionsAsync();
                
                TotalWallpapersText.Text = wallpapers.Count().ToString();
                FavoritesText.Text = favorites.Count().ToString();
                CollectionsText.Text = collections.Count().ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load statistics");
                _snackbarService.Show("Error", "Failed to load statistics", ControlAppearance.Danger, null, TimeSpan.FromSeconds(2));
            }
        }

        private async Task LoadRecentWallpapersAsync()
        {
            try
            {
                var recentWallpapers = await _wallpaperService.GetRecentWallpapersAsync(5);
                var wallpaperList = recentWallpapers.ToList();
                
                if (wallpaperList.Count == 0)
                {
                    return; // Keep the "No recent wallpapers" message
                }

                // Clear the grid
                RecentWallpapersGrid.Children.Clear();
                
                // Create a WrapPanel for the wallpapers
                var wrapPanel = new System.Windows.Controls.WrapPanel 
                { 
                    Orientation = System.Windows.Controls.Orientation.Horizontal,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Left
                };
                
                RecentWallpapersGrid.Children.Add(wrapPanel);
                
                // Add each wallpaper
                foreach (var wallpaper in wallpaperList)
                {
                    var card = new Card
                    {
                        Margin = new Thickness(4),
                        Width = 150,
                        Height = 120
                    };
                    
                    var grid = new Grid();
                    
                    // Add image
                    var image = new System.Windows.Controls.Image
                    {
                        Stretch = System.Windows.Media.Stretch.UniformToFill
                    };
                    
                    if (!string.IsNullOrEmpty(wallpaper.FilePath) && File.Exists(wallpaper.FilePath))
                    {
                        try
                        {
                            var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                            bitmap.BeginInit();
                            bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                            bitmap.UriSource = new Uri(wallpaper.FilePath);
                            bitmap.EndInit();
                            image.Source = bitmap;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error loading wallpaper image: {FilePath}", wallpaper.FilePath);
                        }
                    }
                    
                    grid.Children.Add(image);
                    
                    // Add overlay with name
                    var overlay = new Border
                    {
                        Background = new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromArgb(180, 0, 0, 0)),
                        VerticalAlignment = VerticalAlignment.Bottom,
                        Padding = new Thickness(6)
                    };
                    
                    var nameText = new System.Windows.Controls.TextBlock
                    {
                        Text = wallpaper.Title,
                        TextWrapping = TextWrapping.NoWrap,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White),
                        FontSize = 12
                    };
                    
                    overlay.Child = nameText;
                    grid.Children.Add(overlay);
                    
                    // Set the card content and add to panel
                    card.Content = grid;
                    
                    // Add click handler
                    card.PreviewMouseLeftButtonDown += async (s, args) => await ApplyWallpaper(wallpaper.Id);
                    
                    wrapPanel.Children.Add(card);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load recent wallpapers");
                _snackbarService.Show("Error", "Failed to load recent wallpapers", ControlAppearance.Danger, null, TimeSpan.FromSeconds(2));
            }
        }

        private async Task LoadCurrentWallpaperAsync()
        {
            try
            {
                var settings = await _settingsService.LoadSettingsAsync();
                if (string.IsNullOrEmpty(settings.CurrentWallpaperId))
                {
                    return; // No current wallpaper set
                }
                
                var wallpaper = await _wallpaperService.GetWallpaperByIdAsync(settings.CurrentWallpaperId);
                if (wallpaper == null)
                {
                    return;
                }
                
                CurrentWallpaperNameText.Text = wallpaper.Title;
                CurrentWallpaperSourceText.Text = $"Source: {wallpaper.Source}";
                
                if (!string.IsNullOrEmpty(wallpaper.FilePath) && File.Exists(wallpaper.FilePath))
                {
                    try
                    {
                        var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                        bitmap.UriSource = new Uri(wallpaper.FilePath);
                        bitmap.EndInit();
                        CurrentWallpaperImage.Source = bitmap;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error loading current wallpaper image: {FilePath}", wallpaper.FilePath);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load current wallpaper");
                _snackbarService.Show("Error", "Failed to load current wallpaper", ControlAppearance.Danger, null, TimeSpan.FromSeconds(2));
            }
        }

        private async void RandomWallpaperButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Try to show snackbar with try-catch protection
                try
                {
                    _snackbarService?.Show("Wallpaper", "Applying random wallpaper...", ControlAppearance.Info, null, TimeSpan.FromSeconds(2));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to show snackbar: {Message}", ex.Message);
                    // We'll continue without the snackbar
                }
                
                // Log all steps for diagnostics
                _logger.LogInformation("Getting all wallpapers for random selection");
                
                // Safety check - ensure we have wallpapers
                var wallpapers = await _wallpaperService.GetAllWallpapersAsync();
                var availableWallpapers = wallpapers.ToList();
                
                _logger.LogInformation("Wallpapers found: {Count}", availableWallpapers.Count);
                
                // Display diagnostics if no wallpapers are found
                if (availableWallpapers.Count == 0)
                {
                    var message = "No wallpapers available. Please add some wallpapers first!";
                    _logger.LogWarning(message);
                    
                    // Try through snackbar with fallback to MessageBox
                    try
                    {
                        _snackbarService?.Show("Wallpaper", message, ControlAppearance.Caution, null, TimeSpan.FromSeconds(5));
                    }
                    catch
                    {
                        System.Windows.MessageBox.Show(
                            message,
                            "Wallpaper",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Warning);
                    }
                    
                    // Refresh the UI to ensure it reflects the current state
                    await LoadStatisticsAsync();
                    await LoadRecentWallpapersAsync();
                    
                    return;
                }
                
                // Log available wallpapers for diagnostics
                foreach (var wp in availableWallpapers)
                {
                    _logger.LogInformation("Available wallpaper: ID={Id}, Title={Title}, FilePath={FilePath}", 
                        wp.Id, wp.Title, wp.FilePath);
                }
                
                // Get a random wallpaper
                Random random = new Random();
                var randomIndex = random.Next(0, availableWallpapers.Count);
                var selectedWallpaper = availableWallpapers[randomIndex];
                
                _logger.LogInformation("Selected random wallpaper: ID={Id}, Title={Title}, FilePath={FilePath}",
                    selectedWallpaper.Id, selectedWallpaper.Title, selectedWallpaper.FilePath);
                
                // Safety check - ensure wallpaper file exists
                if (string.IsNullOrEmpty(selectedWallpaper.FilePath) || !File.Exists(selectedWallpaper.FilePath))
                {
                    var message = $"Wallpaper file not found: {selectedWallpaper.Title} (Path: {selectedWallpaper.FilePath})";
                    _logger.LogError(message);
                    
                    try
                    {
                        _snackbarService?.Show("Error", message,
                            ControlAppearance.Danger, null, TimeSpan.FromSeconds(5));
                    }
                    catch
                    {
                        System.Windows.MessageBox.Show(
                            message,
                            "Error",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Error);
                    }
                    return;
                }
                
                // Apply the wallpaper with comprehensive exception handling
                try
                {
                    _logger.LogInformation("Applying wallpaper: {Id}", selectedWallpaper.Id);
                    var success = await _wallpaperService.ApplyWallpaperAsync(selectedWallpaper.Id);
                    
                    if (success)
                    {
                        var message = $"Applied wallpaper: {selectedWallpaper.Title}";
                        _logger.LogInformation(message);
                        
                        try
                        {
                            _snackbarService?.Show("Success", message,
                                ControlAppearance.Success, null, TimeSpan.FromSeconds(2));
                        }
                        catch
                        {
                            System.Windows.MessageBox.Show(
                                message,
                                "Success",
                                System.Windows.MessageBoxButton.OK,
                                System.Windows.MessageBoxImage.Information);
                        }
                            
                        // Refresh current wallpaper display
                        await LoadCurrentWallpaperAsync();
                    }
                    else
                    {
                        var message = "Failed to apply wallpaper";
                        _logger.LogWarning(message + ": {Id}", selectedWallpaper.Id);
                        
                        try
                        {
                            _snackbarService?.Show("Error", message,
                                ControlAppearance.Danger, null, TimeSpan.FromSeconds(5));
                        }
                        catch
                        {
                            System.Windows.MessageBox.Show(
                                message,
                                "Error",
                                System.Windows.MessageBoxButton.OK,
                                System.Windows.MessageBoxImage.Error);
                        }
                    }
                }
                catch (Exception ex)
                {
                    var message = $"Error applying wallpaper: {ex.Message}";
                    _logger.LogError(ex, "Error in ApplyWallpaperAsync: {Id}", selectedWallpaper.Id);
                    
                    System.Windows.MessageBox.Show(
                        message,
                        "Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                // Handle any exception that might occur
                var message = $"Something went wrong: {ex.Message}";
                _logger.LogError(ex, "Critical error in RandomWallpaperButton_Click");
                
                System.Windows.MessageBox.Show(
                    message,
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private async void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Open file dialog to select an image
                var openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Select Wallpaper Image",
                    Filter = "Image files|*.jpg;*.jpeg;*.png;*.bmp|All files|*.*",
                    Multiselect = false
                };
                
                if (openFileDialog.ShowDialog() == true)
                {
                    try
                    {
                        _snackbarService?.Show("Wallpaper", "Importing wallpaper...", ControlAppearance.Info, null, TimeSpan.FromSeconds(2));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Failed to show snackbar: {Message}", ex.Message);
                    }
                    
                    string filePath = openFileDialog.FileName;
                    string fileName = Path.GetFileName(filePath);
                    
                    // Create a proper wallpaper with a unique ID
                    var wallpaper = new Wallpaper
                    {
                        Id = Guid.NewGuid().ToString(),
                        Title = Path.GetFileNameWithoutExtension(fileName),
                        Name = Path.GetFileNameWithoutExtension(fileName),
                        FilePath = filePath,
                        Width = 1920, // Default width
                        Height = 1080, // Default height
                        Source = WallpaperSource.Local,
                        CreatedAt = DateTime.Now,
                        LastUsedAt = DateTime.Now,
                        IsFavorite = false,
                        Tags = new List<string> { "imported" }
                    };
                    
                    // Use SaveWallpaperAsync to properly add the wallpaper to the database
                    bool success = await _wallpaperService.SaveWallpaperAsync(wallpaper);
                    
                    if (success)
                    {
                        try
                        {
                            _snackbarService?.Show("Success", $"Imported wallpaper: {wallpaper.Title}", ControlAppearance.Success, null, TimeSpan.FromSeconds(2));
                        }
                        catch
                        {
                            System.Windows.MessageBox.Show(
                                $"Imported wallpaper: {wallpaper.Title}",
                                "Success",
                                System.Windows.MessageBoxButton.OK,
                                System.Windows.MessageBoxImage.Information);
                        }
                        _logger.LogInformation("Imported wallpaper: {WallpaperId}", wallpaper.Id);
                        
                        // Refresh the UI
                        await LoadStatisticsAsync();
                        await LoadRecentWallpapersAsync();
                        await LoadCurrentWallpaperAsync();
                        
                        // Force refresh of available wallpapers
                        var wallpapers = await _wallpaperService.GetAllWallpapersAsync();
                        _logger.LogInformation("Refreshed wallpapers list, count: {Count}", wallpapers.Count());
                    }
                    else
                    {
                        System.Windows.MessageBox.Show(
                            "Failed to save the wallpaper to the database.",
                            "Error",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Error);
                        _logger.LogError("Failed to save wallpaper to database");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to import wallpaper: {ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                _logger.LogError(ex, "Error importing wallpaper");
            }
        }
        
        private void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            _snackbarService.Show("Coming Soon", "This feature is coming soon!", ControlAppearance.Info, null, TimeSpan.FromSeconds(2));
        }

        private async Task<bool> ApplyWallpaper(string wallpaperId)
        {
            try
            {
                var success = await _wallpaperService.ApplyWallpaperAsync(wallpaperId);
                if (success)
                {
                    var wallpaper = await _wallpaperService.GetWallpaperByIdAsync(wallpaperId);
                    if (wallpaper != null)
                    {
                        _snackbarService.Show("Success", $"Applied wallpaper: {wallpaper.Title}", ControlAppearance.Success, null, TimeSpan.FromSeconds(2));
                    }
                    else
                    {
                        _snackbarService.Show("Success", "Wallpaper applied successfully", ControlAppearance.Success, null, TimeSpan.FromSeconds(2));
                    }
                    _logger.LogInformation("Applied wallpaper: {WallpaperId}", wallpaperId);
                    
                    // Refresh current wallpaper display
                    await LoadCurrentWallpaperAsync();
                    
                    return true;
                }
                else
                {
                    _snackbarService.Show("Error", "Failed to apply wallpaper", ControlAppearance.Danger, null, TimeSpan.FromSeconds(2));
                    _logger.LogWarning("Failed to apply wallpaper: {WallpaperId}", wallpaperId);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _snackbarService.Show("Error", $"Failed to apply wallpaper: {ex.Message}", ControlAppearance.Danger, null, TimeSpan.FromSeconds(2));
                _logger.LogError(ex, "Error applying wallpaper: {WallpaperId}", wallpaperId);
                return false;
            }
        }
    }
} 