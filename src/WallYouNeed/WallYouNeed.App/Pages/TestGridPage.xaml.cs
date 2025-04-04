using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;
using WallYouNeed.Core.Models;

namespace WallYouNeed.App.Pages
{
    /// <summary>
    /// Interaction logic for TestGridPage.xaml
    /// </summary>
    public partial class TestGridPage : Page
    {
        private readonly ILogger<TestGridPage> _logger;
        private ObservableCollection<WallpaperItem> _wallpapers;
        private double _itemWidth = 300; // Default width for each wallpaper item
        private double _itemHeight = 180; // Default height for each wallpaper item
        private const int ScrollThreshold = 200; // Threshold for infinite scrolling

        // Simulated test data for wallpapers
        private readonly List<string> _resolutions = new List<string> { "4K", "5K", "8K" };
        private readonly Random _random = new Random();

        public TestGridPage(ILogger<TestGridPage> logger = null)
        {
            _logger = logger;
            _logger?.LogInformation("TestGridPage constructor called");

            InitializeComponent();
            _wallpapers = new ObservableCollection<WallpaperItem>();

            // Register events
            Loaded += TestGridPage_Loaded;
            SizeChanged += TestGridPage_SizeChanged;
        }

        private void TestGridPage_Loaded(object sender, RoutedEventArgs e)
        {
            _logger?.LogInformation("TestGridPage loaded");
            
            // Show loading indicators
            StatusTextBlock.Visibility = Visibility.Visible;
            LoadingProgressBar.Visibility = Visibility.Visible;

            // Initialize the grid with test data
            InitializeTestGrid();
        }

        private void TestGridPage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _logger?.LogInformation($"Window size changed to: {e.NewSize.Width}x{e.NewSize.Height}");
            
            // Update the layout when the window size changes
            AdjustItemSizes();
        }

        private async void InitializeTestGrid()
        {
            _logger?.LogInformation("Initializing test grid");
            
            try
            {
                // Clear existing items
                WallpaperContainer.Children.Clear();
                _wallpapers.Clear();

                // Generate test data
                for (int i = 0; i < 20; i++)
                {
                    await AddWallpaperItem();
                }

                // Hide loading indicators
                StatusTextBlock.Visibility = Visibility.Collapsed;
                LoadingProgressBar.Visibility = Visibility.Collapsed;

                // Adjust item sizes based on current window width
                AdjustItemSizes();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error initializing test grid");
                StatusTextBlock.Text = "Error loading wallpapers";
                StatusTextBlock.Visibility = Visibility.Visible;
                LoadingProgressBar.Visibility = Visibility.Collapsed;
            }
        }

        private async Task AddWallpaperItem()
        {
            // Create a new wallpaper item with random properties
            var wallpaper = new WallpaperItem
            {
                ImageUrl = GetRandomImageUrl(),
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
                Height = _itemHeight
            };

            // Create a grid to hold the content
            var grid = new Grid();
            border.Child = grid;

            // Create and add the image
            var image = new System.Windows.Controls.Image
            {
                Source = new BitmapImage(new Uri(wallpaper.ImageUrl)),
                Stretch = Stretch.Fill
            };
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
                    Margin = new Thickness(0, 30, 20, 0)
                };
                grid.Children.Add(aiPanel);
            }

            // Add stats
            var statsPanel = new StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                VerticalAlignment = System.Windows.VerticalAlignment.Bottom,
                Margin = new Thickness(8)
            };
            
            // Add likes counter
            var likesPanel = new StackPanel 
            { 
                Orientation = System.Windows.Controls.Orientation.Horizontal, 
                Margin = new Thickness(0, 0, 10, 0) 
            };
            var likesIcon = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M12,21.35L10.55,20.03C5.4,15.36 2,12.27 2,8.5C2,5.41 4.42,3 7.5,3C9.24,3 10.91,3.81 12,5.08C13.09,3.81 14.76,3 16.5,3C19.58,3 22,5.41 22,8.5C22,12.27 18.6,15.36 13.45,20.03L12,21.35Z"),
                Fill = new SolidColorBrush(Colors.White),
                Width = 14,
                Height = 14,
                Margin = new Thickness(0, 0, 4, 0)
            };
            var likesText = new TextBlock { Text = wallpaper.Likes.ToString(), Foreground = new SolidColorBrush(Colors.White) };
            likesPanel.Children.Add(likesIcon);
            likesPanel.Children.Add(likesText);
            statsPanel.Children.Add(likesPanel);
            
            // Add downloads counter
            var downloadsPanel = new StackPanel 
            { 
                Orientation = System.Windows.Controls.Orientation.Horizontal 
            };
            var downloadsIcon = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M5,20H19V18H5M19,9H15V3H9V9H5L12,16L19,9Z"),
                Fill = new SolidColorBrush(Colors.White),
                Width = 14,
                Height = 14,
                Margin = new Thickness(0, 0, 4, 0)
            };
            var downloadsText = new TextBlock { Text = wallpaper.Downloads.ToString(), Foreground = new SolidColorBrush(Colors.White) };
            downloadsPanel.Children.Add(downloadsIcon);
            downloadsPanel.Children.Add(downloadsText);
            statsPanel.Children.Add(downloadsPanel);
            
            // Add a semi-transparent overlay for the stats
            var statsBg = new Border
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(128, 0, 0, 0)),
                CornerRadius = new CornerRadius(4),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                VerticalAlignment = System.Windows.VerticalAlignment.Bottom,
                Margin = new Thickness(0),
                Padding = new Thickness(8, 4, 8, 4),
                Child = statsPanel
            };
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
        }

        private void MainScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // Implement infinite scrolling logic
            if (e.VerticalOffset + e.ViewportHeight + ScrollThreshold >= e.ExtentHeight)
            {
                LoadMoreWallpapers();
            }
        }

        private async void LoadMoreWallpapers()
        {
            _logger?.LogInformation("Loading more wallpapers");
            
            // Show loading status
            StatusTextBlock.Text = "Loading more wallpapers...";
            StatusTextBlock.Visibility = Visibility.Visible;
            LoadingProgressBar.Visibility = Visibility.Visible;

            try
            {
                // Simulate loading delay
                await Task.Delay(500);

                // Add more wallpapers
                for (int i = 0; i < 10; i++)
                {
                    await AddWallpaperItem();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading more wallpapers");
            }
            finally
            {
                // Hide loading indicators
                StatusTextBlock.Visibility = Visibility.Collapsed;
                LoadingProgressBar.Visibility = Visibility.Collapsed;
            }
        }

        private void ImageBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is WallpaperItem wallpaper)
            {
                _logger?.LogInformation($"Wallpaper clicked: {wallpaper.ResolutionLabel}");
                
                // Show a popup with wallpaper details
                System.Windows.MessageBox.Show($"Clicked on {wallpaper.ResolutionLabel} wallpaper\nResolution: {wallpaper.Resolution}\nAI Generated: {wallpaper.IsAI}", 
                    "Wallpaper Details", MessageBoxButton.OK, MessageBoxImage.Information);
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
    /// Model class for wallpaper items in the test grid
    /// </summary>
    public class WallpaperItem
    {
        public string ImageUrl { get; set; } = string.Empty;
        public string Resolution { get; set; } = string.Empty;
        public string ResolutionLabel { get; set; } = string.Empty;
        public bool IsAI { get; set; }
        public int Likes { get; set; }
        public int Downloads { get; set; }
    }
} 