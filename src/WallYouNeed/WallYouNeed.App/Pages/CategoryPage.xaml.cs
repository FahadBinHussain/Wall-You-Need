using Microsoft.Extensions.Logging;
using System;
using System.Windows;
using System.Windows.Controls;
using WallYouNeed.Core.Services.Interfaces;

namespace WallYouNeed.App.Pages
{
    public partial class CategoryPage : Page
    {
        private readonly ILogger<CategoryPage> _logger;
        private readonly IWallpaperService _wallpaperService;
        
        private string _categoryName = "Category";

        public CategoryPage(
            ILogger<CategoryPage> logger,
            IWallpaperService wallpaperService)
        {
            _logger = logger;
            _wallpaperService = wallpaperService;
            
            InitializeComponent();
        }
        
        public void SetCategory(string categoryName)
        {
            _categoryName = categoryName;
            CategoryTitle.Text = categoryName;
            CategoryDescription.Text = $"Browse and manage {categoryName} wallpapers";
            
            _logger.LogInformation("Category page set to: {CategoryName}", categoryName);
        }
        
        private void SortButton_Click(object sender, RoutedEventArgs e)
        {
            // Placeholder for future implementation
            System.Windows.MessageBox.Show(
                "Sort functionality not implemented yet.",
                "Not Implemented",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        
        private void AddWallpaperButton_Click(object sender, RoutedEventArgs e)
        {
            // Placeholder for future implementation
            System.Windows.MessageBox.Show(
                "Add wallpaper functionality not implemented yet.",
                "Not Implemented",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }
}