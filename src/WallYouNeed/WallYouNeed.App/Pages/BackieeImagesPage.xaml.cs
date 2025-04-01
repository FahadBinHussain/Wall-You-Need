using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace WallYouNeed.App.Pages
{
    /// <summary>
    /// Interaction logic for BackieeImagesPage.xaml
    /// </summary>
    public partial class BackieeImagesPage : Page
    {
        private ObservableCollection<BackieeImage> Images { get; set; }

        public BackieeImagesPage()
        {
            InitializeComponent();
            
            Images = new ObservableCollection<BackieeImage>();
            ImagesItemsControl.ItemsSource = Images;
            
            // Load images asynchronously
            Loaded += BackieeImagesPage_Loaded;
        }

        private async void BackieeImagesPage_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadImagesFromMarkdownFile();
        }

        private async Task LoadImagesFromMarkdownFile()
        {
            try
            {
                // Path to the markdown file relative to the solution directory
                string mdFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "backiee_static_images.md");
                
                // Check if file exists and can be accessed
                if (!File.Exists(mdFilePath))
                {
                    // Try an alternative path
                    mdFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backiee_static_images.md");
                    
                    if (!File.Exists(mdFilePath))
                    {
                        System.Windows.MessageBox.Show("Could not find the backiee_static_images.md file.", "File Not Found", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                // Read all lines from the markdown file
                string[] imageUrls = await Task.Run(() => File.ReadAllLines(mdFilePath));

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
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error loading images: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            catch
            {
                // Return the URL if we can't extract the ID
                return url;
            }
        }
    }

    public class BackieeImage
    {
        public string ImageUrl { get; set; }
        public string ImageId { get; set; }
    }
} 