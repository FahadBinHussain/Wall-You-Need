using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;
using WallYouNeed.Core.Models;
using WallYouNeed.Core.Services.Interfaces;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace WallYouNeed.App.Pages
{
    /// <summary>
    /// Interaction logic for CollectionsPage.xaml
    /// </summary>
    public partial class CollectionsPage : Page
    {
        private readonly ILogger<CollectionsPage> _logger;
        private readonly ICollectionService _collectionService;
        private readonly ISnackbarService _snackbarService;

        public CollectionsPage(
            ILogger<CollectionsPage> logger,
            ICollectionService collectionService,
            ISnackbarService snackbarService)
        {
            _logger = logger;
            _collectionService = collectionService;
            _snackbarService = snackbarService;

            InitializeComponent();
            
            this.Loaded += CollectionsPage_Loaded;
            CreateCollectionButton.Click += CreateCollectionButton_Click;
        }

        private async void CollectionsPage_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await LoadCollectionsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading collections");
                _snackbarService.Show("Error", "Failed to load collections", Wpf.Ui.Controls.ControlAppearance.Danger, null, TimeSpan.FromSeconds(2));
            }
        }

        private async System.Threading.Tasks.Task LoadCollectionsAsync()
        {
            var collections = await _collectionService.GetAllCollectionsAsync();
            
            if (collections == null || !collections.Any())
            {
                EmptyStatePanel.Visibility = Visibility.Visible;
                CollectionsPanel.Visibility = Visibility.Collapsed;
                return;
            }

            EmptyStatePanel.Visibility = Visibility.Collapsed;
            CollectionsPanel.Visibility = Visibility.Visible;
            CollectionsPanel.Items.Clear();

            foreach (var collection in collections)
            {
                // Create a collection item
                var border = new Border
                {
                    Margin = new Thickness(0, 0, 16, 16),
                    Width = 250,
                    Height = 200,
                    Background = System.Windows.Application.Current.Resources["CardBackgroundFillColorDefaultBrush"] as System.Windows.Media.Brush,
                    BorderBrush = System.Windows.Application.Current.Resources["ControlStrokeColorDefaultBrush"] as System.Windows.Media.Brush,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8)
                };

                var grid = new Grid();
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(140) });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                // Thumbnail image
                var image = new System.Windows.Controls.Image
                {
                    Stretch = Stretch.UniformToFill
                };
                
                // Use the collection's cover image if available
                if (!string.IsNullOrEmpty(collection.CoverImagePath) && System.IO.File.Exists(collection.CoverImagePath))
                {
                    try
                    {
                        _logger.LogInformation("Loading collection cover image: {Path}", collection.CoverImagePath);
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.UriSource = new Uri(collection.CoverImagePath);
                        bitmap.EndInit();
                        image.Source = bitmap;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to load collection cover image: {Path}", collection.CoverImagePath);
                        // Fallback to placeholder
                        image.Source = new BitmapImage(new Uri($"https://picsum.photos/250/140?random={collection.Id}"));
                    }
                }
                // If no cover image, try to load the first wallpaper in the collection
                else if (collection.WallpaperIds != null && collection.WallpaperIds.Any())
                {
                    try
                    {
                        // Get the first wallpaper in the collection
                        var wallpaper = _collectionService.GetWallpaperFromCollectionAsync(collection.Id, collection.WallpaperIds[0]).Result;
                        
                        if (wallpaper != null && !string.IsNullOrEmpty(wallpaper.FilePath) && System.IO.File.Exists(wallpaper.FilePath))
                        {
                            _logger.LogInformation("Using first wallpaper as collection cover: {Path}", wallpaper.FilePath);
                            var bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.UriSource = new Uri(wallpaper.FilePath);
                            bitmap.EndInit();
                            image.Source = bitmap;
                            
                            // Update the collection's cover image for next time
                            collection.CoverImagePath = wallpaper.FilePath;
                            _collectionService.UpdateCollectionAsync(collection).Wait();
                        }
                        else
                        {
                            // Placeholder image
                            image.Source = new BitmapImage(new Uri($"https://picsum.photos/250/140?random={collection.Id}"));
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to get first wallpaper for collection cover");
                        // Placeholder image
                        image.Source = new BitmapImage(new Uri($"https://picsum.photos/250/140?random={collection.Id}"));
                    }
                }
                else
                {
                    // Placeholder image
                    image.Source = new BitmapImage(new Uri($"https://picsum.photos/250/140?random={collection.Id}"));
                }

                Grid.SetRow(image, 0);
                grid.Children.Add(image);

                // Info panel
                var infoGrid = new Grid { Margin = new Thickness(12, 8, 12, 8) };
                
                var stackPanel = new StackPanel();
                stackPanel.Children.Add(new System.Windows.Controls.TextBlock
                {
                    Text = collection.Name,
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 16
                });
                
                stackPanel.Children.Add(new System.Windows.Controls.TextBlock
                {
                    Text = collection.WallpaperIds != null 
                        ? $"{collection.WallpaperIds.Count} wallpapers" 
                        : "0 wallpapers",
                    Foreground = System.Windows.Application.Current.Resources["TextFillColorSecondaryBrush"] as System.Windows.Media.Brush,
                    FontSize = 12
                });

                infoGrid.Children.Add(stackPanel);

                // Options button
                var optionsButton = new Wpf.Ui.Controls.Button
                {
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center,
                    Appearance = Wpf.Ui.Controls.ControlAppearance.Transparent,
                    Icon = new SymbolIcon { Symbol = SymbolRegular.MoreHorizontal20 },
                    Padding = new Thickness(4)
                };
                
                optionsButton.Click += (s, args) => ShowCollectionOptions(collection);
                
                infoGrid.Children.Add(optionsButton);
                
                Grid.SetRow(infoGrid, 1);
                grid.Children.Add(infoGrid);

                // Make the collection item clickable
                border.MouseLeftButtonDown += (s, args) => ViewCollection(collection);
                border.Cursor = System.Windows.Input.Cursors.Hand;
                
                border.Child = grid;
                CollectionsPanel.Items.Add(border);
            }
        }

        private void ViewCollection(Collection collection)
        {
            _snackbarService.Show("Coming Soon", "Viewing individual collections is coming soon", Wpf.Ui.Controls.ControlAppearance.Info, null, TimeSpan.FromSeconds(2));
            _logger.LogInformation("Viewing collection: {CollectionName} [{CollectionId}]", collection.Name, collection.Id);
        }

        private void ShowCollectionOptions(Collection collection)
        {
            _snackbarService.Show("Coming Soon", "Collection options menu coming soon", Wpf.Ui.Controls.ControlAppearance.Info, null, TimeSpan.FromSeconds(2));
            _logger.LogInformation("Showing options for collection: {CollectionName} [{CollectionId}]", collection.Name, collection.Id);
        }

        private void CreateCollectionButton_Click(object sender, RoutedEventArgs e)
        {
            _snackbarService.Show("Coming Soon", "Creating collections is coming soon", Wpf.Ui.Controls.ControlAppearance.Info, null, TimeSpan.FromSeconds(2));
            _logger.LogInformation("Create collection button clicked");
        }
    }
} 