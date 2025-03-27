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

namespace WallYouNeed.App.Pages
{
    public partial class CategoryPage : Page
    {
        private readonly ILogger<CategoryPage> _logger;
        private readonly IWallpaperService _wallpaperService;
        private readonly Wpf.Ui.ISnackbarService _snackbarService;
        
        private string _categoryName = "Category";
        private List<Wallpaper> _wallpapers = new List<Wallpaper>();

        public CategoryPage(
            ILogger<CategoryPage> logger,
            IWallpaperService wallpaperService,
            Wpf.Ui.ISnackbarService snackbarService)
        {
            _logger = logger;
            _wallpaperService = wallpaperService;
            _snackbarService = snackbarService;
            
            InitializeComponent();
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
                
                // Get wallpapers by tag (the tag is stored in the Category property)
                var wallpapers = await _wallpaperService.GetWallpapersByTagAsync(categoryName);
                _wallpapers = wallpapers.ToList();
                
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
            var resType = DetermineResolutionType(wallpaper.Width, wallpaper.Height);
            
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
            // In a real app, you'd get these values from the wallpaper object
            int likes = new Random().Next(1, 60); // Placeholder
            int downloads = new Random().Next(10, 300); // Placeholder
            
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
                else if (!string.IsNullOrEmpty(wallpaper.SourceUrl))
                {
                    // Load from source URL
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(wallpaper.SourceUrl);
                    bitmap.EndInit();
                    
                    image.Source = bitmap;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading image for wallpaper: {WallpaperId}", wallpaper.Id);
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