using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui;
using Wpf.Ui.Controls;
using WallYouNeed.Core.Models;
using WallYouNeed.Core.Services.Interfaces;
using System.Collections.ObjectModel;

namespace WallYouNeed.App.Pages
{
    public partial class CategoryPage : Page, INavigableView<CategoryPage>
    {
        private readonly ILogger<CategoryPage> _logger;
        private readonly IWallpaperService _wallpaperService;
        private readonly ISnackbarService _snackbarService;
        private readonly IBackieeScraperService _backieeScraperService;
        
        private string _currentCategory = string.Empty;
        
        public ObservableCollection<Wallpaper> Wallpapers { get; } = new();
        
        public string CategoryTitle { get; private set; } = "Category";
        public string CategoryDescription { get; private set; } = "Browse wallpapers by category.";
        
        public CategoryPage ViewModel => this;
        
        public CategoryPage(
            ILogger<CategoryPage> logger,
            IWallpaperService wallpaperService,
            ISnackbarService snackbarService,
            IBackieeScraperService backieeScraperService)
        {
            _logger = logger;
            _wallpaperService = wallpaperService;
            _snackbarService = snackbarService;
            _backieeScraperService = backieeScraperService;
            
            InitializeComponent();
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
                
                // Handle different category types
                switch (category.ToLowerInvariant())
                {
                    case "latest":
                        // Special handling for Latest category since it's no longer supported
                        _logger.LogInformation("Latest category is no longer supported");
                        _snackbarService.Show(
                            "Notice",
                            "The Latest Wallpapers feature is currently unavailable.",
                            ControlAppearance.Caution,
                            null,
                            TimeSpan.FromSeconds(3));
                        
                        // Navigate back to home page
                        if (Window.GetWindow(this) is MainWindow mainWindow &&
                            mainWindow.FindName("MainFrame") is Frame mainFrame && 
                            mainFrame.CanGoBack)
                        {
                            mainFrame.GoBack();
                        }
                        break;
                    case "weekly":
                    case "monthly":
                        // For demo purposes, we'll just use recent wallpapers
                        var recentWallpapers = await _wallpaperService.GetRecentWallpapersAsync(20);
                        foreach (var wallpaper in recentWallpapers)
                        {
                            Wallpapers.Add(wallpaper);
                        }
                        break;
                    default:
                        // For other categories, load existing wallpapers with matching tags
                        var wallpapers = await _wallpaperService.GetWallpapersByTagAsync(category);
                        foreach (var wallpaper in wallpapers)
                        {
                            Wallpapers.Add(wallpaper);
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
    }
}